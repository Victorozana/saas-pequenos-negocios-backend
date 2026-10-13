using System.Net;
using System.Net.Http.Json;
using Agendamento.Api.Application.Quotations.CreateQuotation;
using Agendamento.Api.Application.Quotations.GetQuotations;
using Agendamento.Api.Application.Quotations.UpdateQuotationStatus;
using Agendamento.Api.Domain.Customers;
using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Features.Common;
using Agendamento.IntegrationTests.Features;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Agendamento.IntegrationTests.Tenancy.PostgreSqlRlsTests;

namespace Agendamento.IntegrationTests.Quotations;

public sealed class QuotationLifecycleHttpTests
{
    [Fact(DisplayName = "Agregado publicado Pending exige cliente, itens e tenant e preserva recusas HTTP @spec:AC-090 @spec:AC-092")]
    public async Task LifecycleOverHttp()
    {
        using var factory = new TeamFinancialTestFactory();
        await Prove(factory);
    }

    [Fact(DisplayName = "Ciclo do agregado HTTP persiste decisões e isolamento em PostgreSQL temporário RLS @spec:AC-090 @spec:AC-092")]
    public async Task LifecycleOverPostgreSql()
    {
        await using var database = await Database.Create();
        using var factory = new TeamFinancialTestFactory(database.RestrictedConnection, database.AdministrativeConnection);
        await Prove(factory, database.A, database.B);
    }

    private static async Task Prove(TeamFinancialTestFactory factory, Guid? tenantA = null, Guid? tenantB = null)
    {
        var (client, _, tenant) = await factory.Member("owner_admin", [], tenantA);
        var (foreign, _, otherTenant) = await factory.Member("owner_admin", [], tenantB);
        var customer = Customer.Create(tenant, "Cliente ciclo fictício", "123", null, null, null);
        var foreignCustomer = Customer.Create(otherTenant, "Cliente externo fictício", "123", null, null, null);
        using (var db = factory.Db(tenant)) { db.Customers.Add(customer); await db.SaveChangesAsync(); }
        using (var db = factory.Db(otherTenant)) { db.Customers.Add(foreignCustomer); await db.SaveChangesAsync(); }
        var command = new CreateQuotationCommand($"LIFE-{Guid.NewGuid():N}", customer.Id, DateTime.UtcNow.Date, null,
            "Original", "Entrega", [new(null, "Item A", "un", 25, 2, 5), new(null, "Item B", "hora", 10, 3, 0)],
            new(DepositType.FixedAmount, 15, "Entrada"));
        using (var anonymous = factory.CreateClient())
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/quotations", command)).StatusCode);
        async Task<Guid> Create(CreateQuotationCommand value)
        {
            var response = await client.PostAsJsonAsync("/api/v1/quotations", value with { Code = $"LIFE-{Guid.NewGuid():N}" });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<CreatedIdResponse>())!.Id;
        }
        Task<HttpResponseMessage> Decide(Guid id, QuotationStatus status, HttpClient? actor = null) =>
            (actor ?? client).PatchAsJsonAsync($"/api/v1/quotations/{id}/status", new UpdateQuotationStatusCommand(status));
        var id = await Create(command);
        var detail = (await client.GetFromJsonAsync<QuotationDetailDto>($"/api/v1/quotations/{id}"))!;
        Assert.Equal(customer.Id, detail.CustomerId);
        Assert.Equal(QuotationStatus.Pending, detail.Status);
        Assert.Equal(2, detail.Items.Count);
        Assert.Equal(75, detail.TotalAmount);
        Assert.Equal(15, detail.DepositInfo!.RequiredAmount);
        Assert.Equal(60, detail.DepositInfo.RemainingBalance);
        using (var db = factory.Db(tenant))
        {
            var saved = await db.Quotations.Include(q => q.Items).SingleAsync(q => q.Id == id);
            Assert.Equal(tenant, saved.TenantId); Assert.Equal(customer.Id, saved.CustomerId);
            Assert.Equal(detail.Items.Select(i => i.Id).Order(), saved.Items.Select(i => i.Id).Order());
        }
        foreach (var invalid in new[] { command with { CustomerId = Guid.Empty }, command with { CustomerId = Guid.NewGuid() },
            command with { CustomerId = foreignCustomer.Id }, command with { Items = [] } })
        {
            using var before = factory.Db(tenant);
            var count = await before.Quotations.CountAsync();
            var itemCount = await before.Set<QuotationItem>().CountAsync();
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/quotations", invalid)).StatusCode);
            using var after = factory.Db(tenant);
            Assert.Equal(count, await after.Quotations.CountAsync());
            Assert.Equal(itemCount, await after.Set<QuotationItem>().CountAsync());
        }
        var snapshot = await client.GetStringAsync($"/api/v1/quotations/{id}");
        Assert.Equal(HttpStatusCode.NotFound, (await foreign.GetAsync($"/api/v1/quotations/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await foreign.GetAsync($"/api/v1/quotations/{id}/pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Decide(id, QuotationStatus.Approved, foreign)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await foreign.PostAsync($"/api/v1/quotations/{id}/convert", null)).StatusCode);
        foreach (var invalid in new[] { QuotationStatus.Draft, QuotationStatus.Pending, QuotationStatus.Converted, QuotationStatus.Expired, (QuotationStatus)999 })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await Decide(id, invalid)).StatusCode);
            Assert.Equal(snapshot, await client.GetStringAsync($"/api/v1/quotations/{id}"));
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/v1/quotations/{id}/convert", null)).StatusCode);
        Assert.Equal(snapshot, await client.GetStringAsync($"/api/v1/quotations/{id}"));
        Assert.Equal(HttpStatusCode.NoContent, (await Decide(id, QuotationStatus.Approved)).StatusCode);
        using (var db = factory.Db(tenant)) Assert.Equal(2, await db.ReceivableTitles.CountAsync(r => r.QuotationId == id));
        snapshot = await client.GetStringAsync($"/api/v1/quotations/{id}");
        Assert.Equal(HttpStatusCode.NoContent, (await Decide(id, QuotationStatus.Approved)).StatusCode);
        Assert.Equal(snapshot, await client.GetStringAsync($"/api/v1/quotations/{id}"));
        var conversion = await client.PostAsync($"/api/v1/quotations/{id}/convert", null);
        Assert.Equal(HttpStatusCode.OK, conversion.StatusCode);
        var conversionBody = await conversion.Content.ReadAsStringAsync();
        Assert.Equal(conversionBody, await (await client.PostAsync($"/api/v1/quotations/{id}/convert", null)).Content.ReadAsStringAsync());
        snapshot = await client.GetStringAsync($"/api/v1/quotations/{id}");
        Assert.Equal(QuotationStatus.Converted, (await client.GetFromJsonAsync<QuotationDetailDto>($"/api/v1/quotations/{id}"))!.Status);
        Assert.Equal(HttpStatusCode.NoContent, (await Decide(id, QuotationStatus.Approved)).StatusCode);
        foreach (var invalid in new[] { QuotationStatus.Rejected, QuotationStatus.Canceled, QuotationStatus.Pending })
            Assert.Equal(HttpStatusCode.BadRequest, (await Decide(id, invalid)).StatusCode);
        Assert.Equal(snapshot, await client.GetStringAsync($"/api/v1/quotations/{id}"));
        using (var db = factory.Db(tenant))
        {
            Assert.Equal(2, await db.ReceivableTitles.CountAsync(r => r.QuotationId == id));
            Assert.Equal(1, await db.WorkOrders.CountAsync(w => w.QuotationId == id));
        }
        foreach (var target in new[] { QuotationStatus.Rejected, QuotationStatus.Canceled })
        {
            var finalId = await Create(command);
            Assert.Equal(HttpStatusCode.NoContent, (await Decide(finalId, target)).StatusCode);
            snapshot = await client.GetStringAsync($"/api/v1/quotations/{finalId}");
            Assert.Equal(target, (await client.GetFromJsonAsync<QuotationDetailDto>($"/api/v1/quotations/{finalId}"))!.Status);
            foreach (var invalid in new[] { QuotationStatus.Approved, QuotationStatus.Rejected, QuotationStatus.Pending })
                Assert.Equal(HttpStatusCode.BadRequest, (await Decide(finalId, invalid)).StatusCode);
            Assert.Equal(snapshot, await client.GetStringAsync($"/api/v1/quotations/{finalId}"));
            using var db = factory.Db(tenant);
            Assert.Empty(await db.ReceivableTitles.Where(r => r.QuotationId == finalId).ToListAsync());
        }
        var expiredId = await Create(command with { IssueDate = DateTime.UtcNow.Date.AddDays(-2), ValidUntil = DateTime.UtcNow.Date.AddDays(-1) });
        snapshot = await client.GetStringAsync($"/api/v1/quotations/{expiredId}");
        Assert.Equal(HttpStatusCode.BadRequest, (await Decide(expiredId, QuotationStatus.Approved)).StatusCode);
        Assert.Equal(snapshot, await client.GetStringAsync($"/api/v1/quotations/{expiredId}"));
        Assert.Equal(HttpStatusCode.NoContent, (await Decide(expiredId, QuotationStatus.Canceled)).StatusCode);
        Assert.Equal(QuotationStatus.Canceled, (await client.GetFromJsonAsync<QuotationDetailDto>($"/api/v1/quotations/{expiredId}"))!.Status);
    }
}
