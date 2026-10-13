using System.Net;
using System.Net.Http.Json;
using Agendamento.Api.Application.Quotations.CreateQuotation;
using Agendamento.Api.Application.Quotations.GetQuotations;
using Agendamento.Api.Application.Quotations.UpdateQuotation;
using Agendamento.Api.Application.Quotations.UpdateQuotationStatus;
using Agendamento.Api.Application.Services.CreateService;
using Agendamento.Api.Application.Services.UpdateService;
using Agendamento.Api.Domain.Customers;
using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Features.Common;
using Agendamento.IntegrationTests.Features;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Agendamento.IntegrationTests.Tenancy.PostgreSqlRlsTests;

namespace Agendamento.IntegrationTests.Quotations;

public sealed class QuotationModelHttpTests : IClassFixture<TeamFinancialTestFactory>
{
    private readonly TeamFinancialTestFactory factory;
    public QuotationModelHttpTests(TeamFinancialTestFactory factory) => this.factory = factory;

    [Fact(DisplayName = "Modelo monetário HTTP arredonda frações, valida antes de mutar e isola catálogo @spec:AC-045 @spec:AC-046")]
    public Task MonetaryModelOverHttp() => ProveModel(factory);

    [Fact(DisplayName = "Modelo monetário persiste e reconcilia entrada/saldo em PostgreSQL temporário RLS @spec:AC-045 @spec:AC-046")]
    public async Task MonetaryModelOverPostgreSql()
    {
        await using var database = await Database.Create();
        using var postgresFactory = new TeamFinancialTestFactory(database.RestrictedConnection, database.AdministrativeConnection);
        await ProveModel(postgresFactory, database.A, database.B);
    }

    internal static async Task ProveModel(TeamFinancialTestFactory factory, Guid? tenantA = null, Guid? tenantB = null)
    {
        var (client, _, tenant) = await factory.Member("owner_admin", [], tenantA);
        var (foreignClient, _, _) = await factory.Member("owner_admin", [], tenantB);
        var customer = Customer.Create(tenant, "Cliente modelo fictício", "123", null, null, null);
        using (var db = factory.Db(tenant)) { db.Customers.Add(customer); await db.SaveChangesAsync(); }

        async Task<Guid> CreateService(HttpClient owner) => await CreatedId(await owner.PostAsJsonAsync("/api/v1/services",
            new CreateServiceCommand("Serviço modelo", "Descrição original", "hora", 0.05m)));
        var serviceId = await CreateService(client);
        var foreignServiceId = await CreateService(foreignClient);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/services/{foreignServiceId}")).StatusCode);
        var serviceSnapshot = await client.GetStringAsync($"/api/v1/services/{serviceId}");
        foreach (var price in new[] { -0.01m, 0.001m, 10000000000000000m })
        {
            int before;
            using (var db = factory.Db(tenant)) before = await db.ServiceItems.CountAsync();
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/services",
                new CreateServiceCommand("Inválido", null, "un", price))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/v1/services/{serviceId}",
                new UpdateServiceCommand("Alterado", "Alterada", "m2", price, false))).StatusCode);
            Assert.Equal(serviceSnapshot, await client.GetStringAsync($"/api/v1/services/{serviceId}"));
            using (var db = factory.Db(tenant)) Assert.Equal(before, await db.ServiceItems.CountAsync());
        }

        var create = new CreateQuotationCommand($"MODEL-{Guid.NewGuid():N}", customer.Id, DateTime.UtcNow.Date, null,
            "Original", "Saldo na entrega", [new(serviceId, "Meia hora", "hora", 0.05m, 0.5m, 0)],
            new(DepositType.Percentage, 50m, "Metade"));
        var id = await CreatedId(await client.PostAsJsonAsync("/api/v1/quotations", create));
        var saved = (await client.GetFromJsonAsync<QuotationDetailDto>($"/api/v1/quotations/{id}"))!;
        Assert.Equal(QuotationStatus.Pending, saved.Status);
        Assert.Equal(0.5m, Assert.Single(saved.Items).Quantity);
        Assert.Equal(0.03m, saved.Items.Single().TotalPrice);
        Assert.Equal(0.03m, saved.SubtotalAmount);
        Assert.Equal(0m, saved.DiscountAmount);
        Assert.Equal(0.03m, saved.TotalAmount);
        Assert.Equal(0.02m, saved.DepositInfo!.RequiredAmount);
        Assert.Equal(0.01m, saved.DepositInfo.RemainingBalance);
        Assert.Equal(saved.TotalAmount, saved.DepositInfo.RequiredAmount + saved.DepositInfo.RemainingBalance);
        var snapshot = await client.GetStringAsync($"/api/v1/quotations/{id}");

        var item = create.Items.Single();
        var invalidItems = new[]
        {
            item with { UnitPrice = -1 }, item with { UnitPrice = 0.001m },
            item with { UnitPrice = 10000000000000000m }, item with { Quantity = 0 },
            item with { Quantity = -1 }, item with { Quantity = 0.001m },
            item with { Quantity = 10000000000000000m }, item with { Quantity = decimal.MaxValue },
            item with { DiscountAmount = -0.01m }, item with { DiscountAmount = 0.001m },
            item with { DiscountAmount = 0.04m }, item with { ServiceItemId = foreignServiceId },
            item with { ServiceItemId = Guid.NewGuid() }
        };
        var invalidCommands = invalidItems.Select(i => create with { Items = [i] }).Concat(new[]
        {
            create with { Deposit = new((DepositType)999, 50, null) },
            create with { Deposit = new(DepositType.Percentage, 100.01m, null) },
            create with { Deposit = new(DepositType.Percentage, -1, null) },
            create with { Deposit = new(DepositType.FixedAmount, 0.001m, null) },
            create with { Deposit = new(DepositType.FixedAmount, 0.04m, null) },
            create with { Items = [item with { UnitPrice = 9999999999999999.99m, Quantity = 1 }, item with { UnitPrice = 0.01m, Quantity = 1 }] }
        });
        foreach (var invalid in invalidCommands)
        {
            int quotationsBefore, itemsBefore;
            using (var db = factory.Db(tenant))
            {
                quotationsBefore = await db.Quotations.CountAsync();
                itemsBefore = await db.Set<QuotationItem>().CountAsync();
            }
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/quotations",
                invalid with { Code = $"INVALID-{Guid.NewGuid():N}" })).StatusCode);
            var update = new UpdateQuotationCommand("Changed", invalid.CustomerId, invalid.IssueDate, invalid.ValidUntil,
                "Changed", "Changed", invalid.Items, invalid.Deposit);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/v1/quotations/{id}", update)).StatusCode);
            Assert.Equal(snapshot, await client.GetStringAsync($"/api/v1/quotations/{id}"));
            using (var db = factory.Db(tenant))
            {
                Assert.Equal(quotationsBefore, await db.Quotations.CountAsync());
                Assert.Equal(itemsBefore, await db.Set<QuotationItem>().CountAsync());
                Assert.Empty(await db.ReceivableTitles.Where(r => r.QuotationId == id).ToListAsync());
            }
        }

        Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsJsonAsync($"/api/v1/quotations/{id}/status",
            new UpdateQuotationStatusCommand(QuotationStatus.Approved))).StatusCode);
        using (var db = factory.Db(tenant))
        {
            var titles = await db.ReceivableTitles.Where(r => r.QuotationId == id).OrderBy(r => r.OriginalAmount).ToListAsync();
            Assert.Equal(new[] { 0.01m, 0.02m }, titles.Select(t => t.OriginalAmount).ToArray());
            Assert.Equal(saved.TotalAmount, titles.Sum(t => t.OriginalAmount));
            Assert.All(titles, t => Assert.Equal(tenant, t.TenantId));
        }
        using (var db = factory.Db(tenant))
        {
            var persisted = await db.Quotations.Include(q => q.Items).SingleAsync(q => q.Id == id);
            Assert.Equal(tenant, persisted.TenantId);
            Assert.Equal(0.03m, persisted.TotalAmount);
            Assert.Equal(0.02m, persisted.DepositInfo!.RequiredAmount);
            Assert.Equal(0.01m, persisted.DepositInfo.RemainingBalance);
            Assert.Equal(serviceId, Assert.Single(persisted.Items).ServiceItemId);
        }

        // Exact numeric(18,2) boundaries survive a separate HTTP/database round trip.
        const decimal ceiling = 9999999999999999.99m;
        var boundaryService = await CreatedId(await client.PostAsJsonAsync("/api/v1/services",
            new CreateServiceCommand("Preço limite", null, "un", ceiling)));
        using (var db = factory.Db(tenant))
            Assert.Equal(ceiling, (await db.ServiceItems.SingleAsync(s => s.Id == boundaryService)).BasePrice);
        var boundary = create with
        {
            Code = $"BOUNDARY-{Guid.NewGuid():N}",
            Items = [new(boundaryService, "Preço limite", "un", ceiling, 1, 0)],
            Deposit = new(DepositType.FixedAmount, ceiling, null)
        };
        var boundaryId = await CreatedId(await client.PostAsJsonAsync("/api/v1/quotations", boundary));
        var boundarySaved = (await client.GetFromJsonAsync<QuotationDetailDto>($"/api/v1/quotations/{boundaryId}"))!;
        Assert.Equal(ceiling, boundarySaved.TotalAmount);
        Assert.Equal(ceiling, boundarySaved.DepositInfo!.RequiredAmount);
        Assert.Equal(0, boundarySaved.DepositInfo.RemainingBalance);
        var quantityBoundaryId = await CreatedId(await client.PostAsJsonAsync("/api/v1/quotations", boundary with
        {
            Code = $"QUANTITY-{Guid.NewGuid():N}",
            Items = [new(null, "Quantidade limite", "un", 0, ceiling, 0)],
            Deposit = null
        }));
        using (var db = factory.Db(tenant))
        {
            var amountBoundary = await db.Quotations.Include(q => q.Items).SingleAsync(q => q.Id == boundaryId);
            Assert.Equal(ceiling, amountBoundary.TotalAmount);
            Assert.Equal(ceiling, Assert.Single(amountBoundary.Items).UnitPrice);
            Assert.Equal(ceiling, amountBoundary.DepositInfo!.RequiredAmount);
            var quantityBoundary = await db.Quotations.Include(q => q.Items).SingleAsync(q => q.Id == quantityBoundaryId);
            Assert.Equal(ceiling, Assert.Single(quantityBoundary.Items).Quantity);
            Assert.Equal(0, quantityBoundary.TotalAmount);
        }
    }

    private static async Task<Guid> CreatedId(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreatedIdResponse>())!.Id;
    }
}
