using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Agendamento.Api.Application.Quotations.CreateQuotation;
using Agendamento.Api.Application.Quotations.GetQuotations;
using Agendamento.Api.Application.Quotations.UpdateQuotation;
using Agendamento.Api.Application.Quotations.UpdateQuotationStatus;
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

public sealed class QuotationIntegrationWorkflowTests
{
    [Fact(DisplayName = "Jornada HTTP cria, edita Pending e converte exatamente o conteúdo revisado sem recriar recebíveis @spec:AC-090 @spec:AC-091 @spec:AC-093")]
    public async Task EditedQuotationJourneyInMemory()
    {
        using var factory = new TeamFinancialTestFactory();
        await ProveEditedJourney(factory);
    }

    [Fact(DisplayName = "Jornada HTTP completa materializa edição, arredondamento, OS e decisões em PostgreSQL restrito temporário @spec:AC-090 @spec:AC-091 @spec:AC-092 @spec:AC-093")]
    public async Task EditedQuotationJourneyPostgreSql()
    {
        await using var database = await Database.Create();
        using var factory = new TeamFinancialTestFactory(database.RestrictedConnection, database.AdministrativeConnection);
        await ProveEditedJourney(factory, database.A, database.B);
    }

    private static async Task ProveEditedJourney(TeamFinancialTestFactory factory, Guid? tenantId = null, Guid? otherTenantId = null)
    {
        var (client, _, tenant) = await factory.Member("owner_admin", [], tenantId);
        var originalCustomer = Customer.Create(tenant, "Cliente inicial fictício", "123", null, null, null);
        var revisedCustomer = Customer.Create(tenant, "Cliente revisado fictício", "123", null, null, null);
        var service = ServiceItem.Create(tenant, "Catálogo jornada", null, "hora", 10.03m);
        using (var db = factory.Db(tenant))
        {
            db.Customers.AddRange(originalCustomer, revisedCustomer);
            db.ServiceItems.Add(service);
            await db.SaveChangesAsync();
        }
        var issue = DateTime.UtcNow.Date;
        var create = new CreateQuotationCommand($"JOURNEY-{Guid.NewGuid():N}", originalCustomer.Id,
            issue, issue.AddDays(10), "Antes da revisão", "Inicial",
            [new(null, "Item substituído", "un", 100, 1, 0)], new(DepositType.FixedAmount, 20, "Inicial"));
        var response = await client.PostAsJsonAsync("/api/v1/quotations", create);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<CreatedIdResponse>())!.Id;
        var initial = (await client.GetFromJsonAsync<QuotationDetailDto>($"/api/v1/quotations/{id}"))!;
        Assert.Equal(QuotationStatus.Pending, initial.Status);
        var replacedId = Assert.Single(initial.Items).Id;
        using (var db = factory.Db(tenant))
        {
            Assert.Empty(await db.ReceivableTitles.Where(r => r.QuotationId == id).ToListAsync());
            Assert.Empty(await db.WorkOrders.Where(w => w.QuotationId == id).ToListAsync());
        }
        // 10.03 * 1.50 = 15.045 -> 15.05; discount 0.04 -> 15.01.
        // The deposit rounds 15.01 * 50% = 7.505 -> 7.51; balance = 7.50.
        var edit = new UpdateQuotationCommand(create.Code + "-E", revisedCustomer.Id,
            issue.AddDays(1), issue.AddDays(20), "Revisão final", "Termos revisados",
            [new(service.Id, "Catálogo revisado", "hora", 10.03m, 1.50m, 0.04m),
             new(null, "Manual gratuito", "un", 0, 2.25m, 0)], new(DepositType.Percentage, 50, "Entrada revisada"));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/v1/quotations/{id}", edit)).StatusCode);
        var revised = (await client.GetFromJsonAsync<QuotationDetailDto>($"/api/v1/quotations/{id}"))!;
        Assert.Equal(QuotationStatus.Pending, revised.Status);
        Assert.Equal(revisedCustomer.Id, revised.CustomerId);
        Assert.Equal(edit.Code, revised.Code);
        Assert.Equal(edit.IssueDate, revised.IssueDate);
        Assert.Equal(edit.ValidUntil, revised.ValidUntil);
        Assert.Equal(edit.PaymentTerms, revised.PaymentTerms);
        Assert.Equal(edit.Notes, revised.Notes);
        Assert.Equal(15.05m, revised.SubtotalAmount);
        Assert.Equal(0.04m, revised.DiscountAmount);
        Assert.Equal(15.01m, revised.TotalAmount);
        Assert.Equal(7.51m, revised.DepositInfo!.RequiredAmount);
        Assert.Equal(7.50m, revised.DepositInfo.RemainingBalance);
        Assert.Equal("Entrada revisada", revised.DepositInfo.PaymentNotes);
        Assert.DoesNotContain(revised.Items, item => item.Id == replacedId);
        using (var db = factory.Db(tenant))
        {
            var saved = await db.Quotations.Include(q => q.Items).SingleAsync(q => q.Id == id);
            Assert.Equal(tenant, saved.TenantId);
            Assert.Equal(revisedCustomer.Id, saved.CustomerId);
            Assert.Equal(revised.Items.Select(i => i.Id).Order(), saved.Items.Select(i => i.Id).Order());
            Assert.False(await db.Set<QuotationItem>().AnyAsync(i => i.Id == replacedId));
            Assert.All(saved.Items, item => Assert.Equal(id, item.QuotationId));
        }
        async Task<HttpResponseMessage> Decide(QuotationStatus status) =>
            await client.PatchAsJsonAsync($"/api/v1/quotations/{id}/status", new UpdateQuotationStatusCommand(status));
        Assert.Equal(HttpStatusCode.NoContent, (await Decide(QuotationStatus.Approved)).StatusCode);
        async Task<string> FinancialSnapshot()
        {
            using var db = factory.Db(tenant);
            var titles = await db.ReceivableTitles.Where(r => r.QuotationId == id).OrderBy(r => r.Id).AsNoTracking().ToListAsync();
            Assert.Equal(2, titles.Count);
            Assert.Equal(new[] { 7.50m, 7.51m }, titles.Select(r => r.OriginalAmount).Order());
            Assert.All(titles, title => { Assert.Equal(tenant, title.TenantId); Assert.Equal(revisedCustomer.Id, title.CustomerId); });
            // Project scalar persisted fields, avoiding navigation fix-up differences between contexts.
            return JsonSerializer.Serialize(titles.Select(r => new { r.Id, r.TenantId, r.CustomerId, r.QuotationId, r.Description, r.OriginalAmount, r.DueDate, r.Status }));
        }
        var financial = await FinancialSnapshot();
        var approved = await client.GetStringAsync($"/api/v1/quotations/{id}");
        Assert.Equal(HttpStatusCode.NoContent, (await Decide(QuotationStatus.Approved)).StatusCode);
        Assert.Equal(approved, await client.GetStringAsync($"/api/v1/quotations/{id}"));
        Assert.Equal(financial, await FinancialSnapshot());
        Guid orderId = Guid.Empty;
        foreach (var alternative in new[] { true, false, true, false })
        {
            var route = alternative ? $"/api/v1/work-orders/from-quotation/{id}" : $"/api/v1/quotations/{id}/convert";
            var converted = await client.PostAsync(route, null);
            Assert.Equal(alternative ? HttpStatusCode.Created : HttpStatusCode.OK, converted.StatusCode);
            var returned = (await converted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty(alternative ? "id" : "workOrderId").GetGuid();
            if (orderId == Guid.Empty) orderId = returned;
            Assert.Equal(orderId, returned);
            if (alternative) Assert.Equal($"/api/v1/work-orders/{orderId}", converted.Headers.Location!.OriginalString);
        }
        using (var db = factory.Db(tenant))
        {
            var order = await db.WorkOrders.Include(w => w.Items).SingleAsync(w => w.QuotationId == id);
            Assert.Equal(orderId, order.Id);
            Assert.Equal(tenant, order.TenantId);
            Assert.Equal(revisedCustomer.Id, order.CustomerId);
            Assert.Equal("OS-" + edit.Code, order.Code);
            Assert.Equal(edit.Notes, order.Notes);
            Assert.Equal(WorkOrderStatus.Scheduled, order.Status);
            Assert.Null(order.CompletedAt);
            Assert.Equal(2, order.Items.Count);
            foreach (var item in revised.Items)
            {
                var copied = Assert.Single(order.Items, i => i.ServiceName == item.ServiceName);
                Assert.NotEqual(item.Id, copied.Id);
                Assert.Equal(orderId, copied.WorkOrderId);
                Assert.Equal(item.ServiceItemId, copied.ServiceItemId);
                Assert.Equal(item.Unit, copied.Unit);
                Assert.Equal(item.UnitPrice, copied.UnitPrice);
                Assert.Equal(item.Quantity, copied.Quantity);
                Assert.Equal(item.DiscountAmount, copied.DiscountAmount);
                Assert.Equal(item.TotalPrice, Agendamento.Api.Application.WorkOrders.GetWorkOrderById.WorkOrderItemResponse.FromEntity(copied).TotalAmount);
            }
            Assert.Equal(QuotationStatus.Converted, (await db.Quotations.SingleAsync(q => q.Id == id)).Status);
        }
        var final = await client.GetStringAsync($"/api/v1/quotations/{id}");
        var orderSnapshot = await client.GetStringAsync($"/api/v1/work-orders/{orderId}");
        var (outsider, _, otherTenant) = await factory.Member("owner_admin", [], otherTenantId);
        Assert.NotEqual(tenant, otherTenant);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/v1/quotations/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.PutAsJsonAsync($"/api/v1/quotations/{id}", edit)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await outsider.PostAsync($"/api/v1/quotations/{id}/convert", null)).StatusCode);
        using (var db = factory.Db(otherTenant))
        {
            Assert.False(await db.Quotations.AnyAsync(q => q.Id == id));
            Assert.Empty(await db.Set<QuotationItem>().Where(i => i.QuotationId == id).ToListAsync());
            Assert.False(await db.WorkOrders.AnyAsync(w => w.Id == orderId));
            Assert.Empty(await db.ReceivableTitles.Where(r => r.QuotationId == id).ToListAsync());
        }
        Assert.Equal(HttpStatusCode.NoContent, (await Decide(QuotationStatus.Approved)).StatusCode);
        foreach (var decision in new[] { QuotationStatus.Rejected, QuotationStatus.Canceled, QuotationStatus.Pending })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await Decide(decision)).StatusCode);
            Assert.Equal(final, await client.GetStringAsync($"/api/v1/quotations/{id}"));
            Assert.Equal(orderSnapshot, await client.GetStringAsync($"/api/v1/work-orders/{orderId}"));
            Assert.Equal(financial, await FinancialSnapshot());
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/v1/quotations/{id}", edit with { Notes = "Recusado" })).StatusCode);
        Assert.Equal(final, await client.GetStringAsync($"/api/v1/quotations/{id}"));
        Assert.Equal(financial, await FinancialSnapshot());
    }
}
