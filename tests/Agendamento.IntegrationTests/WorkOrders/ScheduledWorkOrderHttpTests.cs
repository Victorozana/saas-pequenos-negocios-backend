using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Agendamento.Api.Domain.Appointments;
using Agendamento.Api.Domain.Customers;
using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Features.Appointments;
using Agendamento.Api.Features.WorkOrders;
using Agendamento.IntegrationTests.Features;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Agendamento.IntegrationTests.Tenancy.PostgreSqlRlsTests;

namespace Agendamento.IntegrationTests.WorkOrders;

public sealed class ScheduledWorkOrderHttpTests
{
    private static readonly DateTimeOffset Now = new(2032, 11, 6, 1, 0, 0, TimeSpan.Zero);
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }

    [Fact]
    public void TodayUsesSaoPauloMidnight()
    {
        var (start, end) = AppointmentEndpoints.DayRange(Now);
        Assert.Equal(new DateTime(2032, 11, 5, 3, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(start.AddDays(1), end);
    }

    [Fact]
    public async Task TeamsSchedulesAndRetriesAreIsolatedInPostgreSql()
    {
        await using var database = await Database.Create();
        using var factory = new TeamFinancialTestFactory(database.RestrictedConnection, database.AdministrativeConnection) { Clock = new Clock() };
        var (owner, _, tenant) = await factory.Member("owner_admin", [], database.A);
        var (joao, joaoId, _) = await factory.Member("Employee", ["appointments.read", "appointments.write"], tenant);
        var (maria, mariaId, _) = await factory.Member("Employee", ["appointments.read"], tenant);
        var (outsider, foreignId, _) = await factory.Member("owner_admin", [], database.B);

        var customer = Customer.Create(tenant, "Cliente para agendamento", "123", null, null, null);
        var quotation = Quotation.Create(tenant, $"AGENDA-{Guid.NewGuid():N}", customer.Id, DateTime.UtcNow, null, null, null);
        quotation.AddItem(QuotationItem.Create(null, "Serviço de teste", "un", 125m, 1, 0));
        quotation.MarkAsPending();
        using (var db = factory.Db(tenant))
        {
            db.Customers.Add(customer);
            db.Quotations.Add(quotation);
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PatchAsJsonAsync($"/api/v1/quotations/{quotation.Id}/status", new { newStatus = 3 })).StatusCode);

        var path = $"/api/v1/work-orders/from-quotation/{quotation.Id}";
        var first = new ScheduleWorkOrderRequest(AppointmentType.TechnicalVisit, Now.AddHours(-23), Now.AddHours(-21), [joaoId], null, null);
        var second = first with { Type = AppointmentType.Installation, StartTime = Now.AddDays(1), EndTime = Now.AddDays(1).AddHours(1), ParticipantIds = [mariaId] };
        var shared = first with { StartTime = Now.AddMinutes(-30), EndTime = Now.AddHours(3), ParticipantIds = [joaoId, mariaId] };
        Assert.Equal(HttpStatusCode.Forbidden, (await joao.PostOrderAsync(path, Guid.NewGuid(), first)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostOrderAsync(path, Guid.NewGuid(), first with { ParticipantIds = [] })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostOrderAsync(path, Guid.NewGuid(), first with { ParticipantIds = [foreignId] })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.PostOrderAsync(path, Guid.NewGuid(), first with { ParticipantIds = [foreignId] })).StatusCode);
        var key = Guid.NewGuid();
        var firstResponses = await Task.WhenAll(owner.PostOrderAsync(path, key, first), owner.PostOrderAsync(path, key, first));
        var firstId = await Created(firstResponses[0]);
        Assert.Equal(firstId, await Created(firstResponses[1]));
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostOrderAsync(path, key, second)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostOrderAsync(path, Guid.NewGuid(), first with { ParticipantIds = [joaoId, mariaId] })).StatusCode);
        var secondId = await Created(await owner.PostOrderAsync(path, Guid.NewGuid(), second));
        var sharedId = await Created(await owner.PostOrderAsync(path, Guid.NewGuid(), shared));
        Assert.Equal(3, new[] { firstId, secondId, sharedId }.Distinct().Count());

        var ownerToday = await owner.GetFromJsonAsync<List<AgendaVisit>>("/api/v1/appointments/today");
        Assert.Equal(new[] { firstId, sharedId }, ownerToday!.Select(a => a.WorkOrderId!.Value).ToArray());
        var mariaToday = await maria.GetFromJsonAsync<List<AgendaVisit>>("/api/v1/appointments/today");
        Assert.Equal(sharedId, Assert.Single(mariaToday!).WorkOrderId);
        Assert.Equal(2, mariaToday![0].Participants.Count);
        Assert.Equal(HttpStatusCode.NotFound, (await joao.GetAsync($"/api/v1/appointments?assignedToUserId={mariaId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await joao.GetAsync($"/api/v1/work-orders/{secondId}")).StatusCode);
        var detail = await joao.GetFromJsonAsync<JsonElement>($"/api/v1/work-orders/{sharedId}");
        Assert.Empty(detail.GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/v1/work-orders/{sharedId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await joao.PutAsJsonAsync($"/api/v1/work-orders/{sharedId}/schedule", shared)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync($"/api/v1/work-orders/{secondId}/schedule", shared)).StatusCode);


        var ownOrders = await joao.GetFromJsonAsync<WorkOrderPage>("/api/v1/work-orders");
        Assert.Equal(2, ownOrders!.Total);
        Assert.DoesNotContain(ownOrders.Items, w => w.Id == secondId);

        using var stored = factory.Db(tenant);
        var legacy = await stored.WorkOrders.AsNoTracking().SingleAsync(w => w.QuotationId != quotation.Id);
        var legacySchedule = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/work-orders/{legacy.Id}");
        Assert.True(legacySchedule.GetProperty("schedule").GetProperty("needsRegularization").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PatchAsJsonAsync($"/api/v1/work-orders/{legacy.Id}/status", new { status = 2 })).StatusCode);
        var repair = first with { StartTime = Now.AddDays(5), EndTime = Now.AddDays(5).AddHours(1) };
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PutAsJsonAsync($"/api/v1/work-orders/{legacy.Id}/schedule", repair)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PatchAsJsonAsync($"/api/v1/work-orders/{legacy.Id}/status", new { status = 2 })).StatusCode);

        var invalidLink = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => stored.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"WorkOrderParticipants\" (tenant_id, \"WorkOrderId\", \"UserId\") VALUES ({tenant}, {sharedId}, {foreignId})"));
        Assert.Equal(Npgsql.PostgresErrorCodes.ForeignKeyViolation, invalidLink.SqlState);
        Assert.Equal(3, await stored.WorkOrders.CountAsync(w => w.QuotationId == quotation.Id));
        Assert.Equal(125m, await stored.ReceivableTitles.Where(r => r.QuotationId == quotation.Id).SumAsync(r => r.OriginalAmount));
        using var foreign = factory.Db(database.B);
        Assert.False(await foreign.WorkOrderParticipants.IgnoreQueryFilters().AnyAsync(p => p.WorkOrderId == sharedId));
    }

    private static async Task<Guid> Created(HttpResponseMessage response)
    {
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Expected 201, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
}
