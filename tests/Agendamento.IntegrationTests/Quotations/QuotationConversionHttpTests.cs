using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Agendamento.Api.Application.Quotations.CreateQuotation;
using Agendamento.Api.Application.Quotations.GetQuotations;
using Agendamento.Api.Application.WorkOrders.GetWorkOrderById;
using Agendamento.Api.Domain.Customers;
using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Domain.Services;
using Agendamento.Api.Domain.WorkOrders;
using Agendamento.Api.Features.Common;
using Agendamento.IntegrationTests.Features;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Agendamento.IntegrationTests.Tenancy.PostgreSqlRlsTests;

namespace Agendamento.IntegrationTests.Quotations;

public sealed class QuotationConversionHttpTests
{
    [Fact(DisplayName = "Conversão nas duas rotas preserva contrato, autorização, cópia e idempotência HTTP @spec:AC-091 @spec:AC-092 @spec:AC-093")]
    public async Task InMemoryContract()
    {
        using var factory = new TeamFinancialTestFactory();
        await Prove(factory);
    }

    [Fact(DisplayName = "Conversão nas duas rotas persiste cópia integral e isolamento em PostgreSQL restrito @spec:AC-091 @spec:AC-092 @spec:AC-093")]
    public async Task PostgreSqlContract()
    {
        await using var database = await Database.Create();
        using var factory = new TeamFinancialTestFactory(database.RestrictedConnection, database.AdministrativeConnection);
        await Prove(factory, database.A, database.B);
    }

    private static string Route(Guid id, bool alternative) => alternative
        ? $"/api/v1/work-orders/from-quotation/{id}" : $"/api/v1/quotations/{id}/convert";

    private static async Task Prove(TeamFinancialTestFactory factory, Guid? tenantA = null, Guid? tenantB = null)
    {
        var (owner, _, tenant) = await factory.Member("owner_admin", [], tenantA);
        var (foreign, _, otherTenant) = await factory.Member("owner_admin", [], tenantB);
        var (writer, _, _) = await factory.Member("Employee", ["sales.write"], tenant);
        var (reader, _, _) = await factory.Member("Employee", ["sales.read"], tenant);
        var (neither, _, _) = await factory.Member("Employee", [], tenant);
        using var anonymous = factory.CreateClient();
        var customer = Customer.Create(tenant, "Cliente conversão fictício", "123", null, null, null);
        var service = ServiceItem.Create(tenant, "Catálogo conversão", null, "hora", 10.03m);
        using (var db = factory.Db(tenant)) { db.Customers.Add(customer); db.ServiceItems.Add(service); await db.SaveChangesAsync(); }
        var command = new CreateQuotationCommand($"CONV-{Guid.NewGuid():N}", customer.Id, DateTime.UtcNow.Date,
            DateTime.UtcNow.Date.AddDays(10), new string('n', 2000), "Condição vinculada",
            [new(service.Id, new string('s', 300), new string('u', 60), 10.03m, 1.5m, 0.04m), new(null, "Manual gratuito", "un", 0, 2.25m, 0)],
            new(DepositType.Percentage, 33.33m, "Entrada preservada"));
        async Task<Guid> Create()
        {
            var result = await owner.PostAsJsonAsync("/api/v1/quotations", command with { Code = $"CONV-{Guid.NewGuid():N}".PadRight(50, 'x') });
            Assert.Equal(HttpStatusCode.Created, result.StatusCode);
            return (await result.Content.ReadFromJsonAsync<CreatedIdResponse>())!.Id;
        }
        foreach (var alternative in new[] { false, true })
        {
            var id = await Create();
            var path = Route(id, alternative);
            var pending = await owner.GetStringAsync($"/api/v1/quotations/{id}");
            foreach (var (actor, status) in new[] { (anonymous, HttpStatusCode.Unauthorized), (reader, HttpStatusCode.Forbidden), (neither, HttpStatusCode.Forbidden) })
            {
                var denied = await actor.PostAsync(path, null);
                Assert.Equal(status, denied.StatusCode);
                if (status == HttpStatusCode.Unauthorized) { Assert.Equal("", await denied.Content.ReadAsStringAsync()); Assert.Contains(denied.Headers.WwwAuthenticate, h => h.Scheme == "Bearer"); }
                else await SafeProblem(denied, status);
            }
            await SafeProblem(await writer.PostAsync(path, null));
            Assert.Equal(pending, await owner.GetStringAsync($"/api/v1/quotations/{id}"));
            var missing = Guid.NewGuid();
            var missingBody = await (await writer.PostAsync(Route(missing, alternative), null)).Content.ReadAsStringAsync();
            foreach (var inaccessible in new[] { Guid.Empty, missing, id })
            {
                var actor = inaccessible == id ? foreign : writer;
                using var request = new HttpRequestMessage(HttpMethod.Post, Route(inaccessible, alternative) + $"?tenantId={tenant}");
                request.Headers.Add("X-Tenant-Id", tenant.ToString());
                request.Content = new StringContent($"{{\"tenantId\":\"{tenant}\"}}", Encoding.UTF8, "application/json");
                var denied = await actor.SendAsync(request);
                await SafeProblem(denied);
                Assert.Equal(missingBody, await denied.Content.ReadAsStringAsync());
            }
            var malformed = alternative ? "/api/v1/work-orders/from-quotation/not-a-guid" : "/api/v1/quotations/not-a-guid/convert";
            var malformedResponse = await writer.PostAsync(malformed, null);
            Assert.Equal(HttpStatusCode.NotFound, malformedResponse.StatusCode); Assert.Equal("", await malformedResponse.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.NoContent, (await writer.PatchAsJsonAsync($"/api/v1/quotations/{id}/status", new { newStatus = 3 })).StatusCode);
            var before = (await owner.GetFromJsonAsync<QuotationDetailDto>($"/api/v1/quotations/{id}"))!;
            string financialBefore;
            using (var db = factory.Db(tenant)) financialBefore = JsonSerializer.Serialize(await db.ReceivableTitles.Where(r => r.QuotationId == id).OrderBy(r => r.Id).AsNoTracking().ToListAsync());
            Assert.Equal(2, JsonSerializer.Deserialize<JsonElement>(financialBefore).GetArrayLength());
            var started = DateTime.UtcNow;
            Guid orderId = Guid.Empty;
            // These routes have no body parameter: even malformed JSON and unsupported media are ignored.
            foreach (var (routeAlternative, raw, media) in new[] { (alternative, "{", "application/json"), (!alternative, "tenant spoof", "text/plain"), (alternative, "null", "application/json"), (!alternative, "[]", "application/json") })
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, Route(id, routeAlternative) + $"?tenantId={otherTenant}");
                request.Headers.Add("X-Tenant-Id", otherTenant.ToString());
                request.Headers.Accept.ParseAdd("application/pdf");
                request.Content = new StringContent(raw, Encoding.UTF8, media);
                var converted = await writer.SendAsync(request);
                Assert.Equal(routeAlternative ? HttpStatusCode.Created : HttpStatusCode.OK, converted.StatusCode);
                Assert.Equal("application/json", converted.Content.Headers.ContentType!.MediaType);
                var body = await converted.Content.ReadFromJsonAsync<JsonElement>();
                var returned = body.GetProperty(routeAlternative ? "id" : "workOrderId").GetGuid();
                Assert.Single(body.EnumerateObject());
                if (orderId == Guid.Empty) orderId = returned;
                Assert.Equal(orderId, returned);
                if (routeAlternative) Assert.Equal($"/api/v1/work-orders/{orderId}", converted.Headers.Location!.OriginalString);
                else Assert.Null(converted.Headers.Location);
            }
            var response = await owner.GetAsync($"/api/v1/work-orders/{orderId}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var order = (await response.Content.ReadFromJsonAsync<WorkOrderResponse>())!;
            Assert.Equal(orderId, order.Id); Assert.Equal(id, order.QuotationId); Assert.Equal(customer.Id, order.CustomerId);
            Assert.Equal($"OS-{before.Code}", order.Code); Assert.Equal(before.Notes, order.Notes);
            Assert.Equal("Scheduled", order.Status); Assert.Null(order.CompletedAt);
            Assert.InRange(order.CreatedAt, started.AddSeconds(-1), DateTime.UtcNow.AddSeconds(1)); Assert.Equal(DateTimeKind.Utc, order.CreatedAt.Kind);
            Assert.Equal(before.Items.Count, order.Items.Count); Assert.Equal(before.TotalAmount, order.Items.Sum(i => i.TotalAmount));
            foreach (var original in before.Items)
            {
                var copied = Assert.Single(order.Items, i => i.ServiceName == original.ServiceName);
                Assert.NotEqual(original.Id, copied.Id); Assert.NotEqual(Guid.Empty, copied.Id);
                Assert.Equal(original.ServiceItemId, copied.ServiceItemId); Assert.Equal(original.Unit, copied.Unit);
                Assert.Equal(original.Quantity, copied.Quantity); Assert.Equal(original.UnitPrice, copied.UnitPrice);
                Assert.Equal(original.DiscountAmount, copied.DiscountAmount); Assert.Equal(original.TotalPrice, copied.TotalAmount);
            }
            var convertedSnapshot = await owner.GetStringAsync($"/api/v1/quotations/{id}");
            Assert.Equal(QuotationStatus.Converted, (await owner.GetFromJsonAsync<QuotationDetailDto>($"/api/v1/quotations/{id}"))!.Status);
            Assert.Equal(HttpStatusCode.NoContent, (await writer.PatchAsJsonAsync($"/api/v1/quotations/{id}/status", new { newStatus = 3 })).StatusCode);
            await SafeProblem(await writer.PatchAsJsonAsync($"/api/v1/quotations/{id}/status", new { newStatus = 4 }));
            Assert.Equal(convertedSnapshot, await owner.GetStringAsync($"/api/v1/quotations/{id}"));
            Assert.Equal(HttpStatusCode.NotFound, (await foreign.GetAsync($"/api/v1/work-orders/{orderId}")).StatusCode);
            foreach (var inaccessibleRoute in new[] { false, true })
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, Route(id, inaccessibleRoute) + $"?tenantId={tenant}");
                request.Headers.Add("X-Tenant-Id", tenant.ToString());
                var denied = await foreign.SendAsync(request);
                await SafeProblem(denied);
                Assert.DoesNotContain(orderId.ToString(), await denied.Content.ReadAsStringAsync());
            }
            using (var db = factory.Db(tenant))
            {
                var saved = await db.WorkOrders.Include(w => w.Items).SingleAsync(w => w.QuotationId == id);
                Assert.Equal(orderId, saved.Id); Assert.Equal(tenant, saved.TenantId); Assert.Equal(WorkOrderStatus.Scheduled, saved.Status);
                Assert.All(saved.Items, item => Assert.Equal(orderId, item.WorkOrderId)); Assert.Equal(2, saved.Items.Count);
                Assert.Equal(financialBefore, JsonSerializer.Serialize(await db.ReceivableTitles.Where(r => r.QuotationId == id).OrderBy(r => r.Id).AsNoTracking().ToListAsync()));
            }
            // Starting and completing work are explicit operations. Replay must
            // preserve their notes and timestamps instead of resetting the OS.
            Assert.Equal(HttpStatusCode.NoContent, (await writer.PatchAsJsonAsync($"/api/v1/work-orders/{orderId}/status", new { status = 2, notes = "Início manual" })).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await writer.PatchAsJsonAsync($"/api/v1/work-orders/{orderId}/status", new { status = 4, notes = "Conclusão manual" })).StatusCode);
            var completedSnapshot = await owner.GetStringAsync($"/api/v1/work-orders/{orderId}");
            foreach (var replayRoute in new[] { false, true })
            {
                var replay = await writer.PostAsync(Route(id, replayRoute), null);
                Assert.Equal(replayRoute ? HttpStatusCode.Created : HttpStatusCode.OK, replay.StatusCode);
                Assert.Equal(orderId, (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty(replayRoute ? "id" : "workOrderId").GetGuid());
            }
            Assert.Equal(completedSnapshot, await owner.GetStringAsync($"/api/v1/work-orders/{orderId}"));
            Assert.Equal(convertedSnapshot, await owner.GetStringAsync($"/api/v1/quotations/{id}"));
        }
        // Internal Draft/Expired records cannot be produced publicly; seed through the domain with tenant-aware item guards.
        foreach (var state in new[] { QuotationStatus.Draft, QuotationStatus.Pending, QuotationStatus.Rejected, QuotationStatus.Expired, QuotationStatus.Canceled })
        {
            var expired = state == QuotationStatus.Expired;
            var q = Quotation.Create(tenant, $"STATE-{Guid.NewGuid():N}", customer.Id, DateTime.UtcNow.Date.AddDays(-2), expired ? DateTime.UtcNow.Date.AddDays(-1) : null, "Preservar", "Termos");
            q.AddItem(QuotationItem.Create(null, "Item estado", "un", 1, 1, 0));
            if (state != QuotationStatus.Draft) q.MarkAsPending();
            if (state == QuotationStatus.Rejected) q.Reject();
            if (expired) q.CheckExpiration();
            if (state == QuotationStatus.Canceled) q.Cancel();
            using (var db = factory.Db(tenant)) { db.Quotations.Add(q); await db.SaveChangesAsync(); }
            var snapshot = await owner.GetStringAsync($"/api/v1/quotations/{q.Id}");
            foreach (var alternative in new[] { false, true }) await SafeProblem(await writer.PostAsync(Route(q.Id, alternative), null));
            Assert.Equal(snapshot, await owner.GetStringAsync($"/api/v1/quotations/{q.Id}"));
            using var after = factory.Db(tenant);
            Assert.Equal(state, (await after.Quotations.SingleAsync(x => x.Id == q.Id)).Status);
            Assert.Empty(await after.WorkOrders.Where(w => w.QuotationId == q.Id).ToListAsync());
            Assert.Empty(await after.ReceivableTitles.Where(r => r.QuotationId == q.Id).ToListAsync());
        }
    }

    private static async Task SafeProblem(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.BadRequest)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)expected, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("title").GetString()));
        Assert.False(body.TryGetProperty("exception", out _)); Assert.False(body.TryGetProperty("stackTrace", out _));
        var raw = body.ToString();
        foreach (var forbidden in new[] { "Npgsql", "ConnectionString", "Password=", "Microsoft.EntityFrameworkCore", "SELECT " }) Assert.DoesNotContain(forbidden, raw, StringComparison.OrdinalIgnoreCase);
    }
}
