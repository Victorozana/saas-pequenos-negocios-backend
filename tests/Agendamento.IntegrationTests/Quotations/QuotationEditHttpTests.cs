using System.Net;
using System.Net.Http.Json;
using Agendamento.Api.Application.Notifications;
using Agendamento.Api.Application.Quotations.CreateQuotation;
using Agendamento.Api.Application.Quotations.GetQuotations;
using Agendamento.Api.Application.Quotations.UpdateQuotation;
using Agendamento.Api.Domain.Customers;
using Agendamento.Api.Domain.Quotations;
using Agendamento.IntegrationTests.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Agendamento.IntegrationTests.Quotations;

public sealed class QuotationEditHttpTests : IClassFixture<TeamFinancialTestFactory>
{
    private readonly TeamFinancialTestFactory factory;
    public QuotationEditHttpTests(TeamFinancialTestFactory factory) => this.factory = factory;

    [Fact(DisplayName = "Orçamento enviado permanece editável pendente, recalcula conteúdo e isola empresas @spec:AC-206 @spec:AC-092")]
    public Task PendingEditPersistsAndIsolates() => ProvePendingEdit(factory);

    internal static async Task ProvePendingEdit(TeamFinancialTestFactory factory, Guid? tenantA = null, Guid? tenantB = null)
    {
        var (client, _, tenant) = await factory.Member("owner_admin", [], tenantA);
        var (other, _, foreignTenant) = await factory.Member("owner_admin", [], tenantB);
        var customer = Customer.Create(tenant, "Cliente fictício", "123", null, null, null);
        var nextCustomer = Customer.Create(tenant, "Outro cliente fictício", "123", null, null, null);
        var foreign = Customer.Create(foreignTenant, "Cliente externo fictício", "123", null, null, null);
        using (var db = factory.Db(tenant)) { db.Customers.AddRange(customer, nextCustomer); await db.SaveChangesAsync(); }
        using (var db = factory.Db(foreignTenant)) { db.Customers.Add(foreign); await db.SaveChangesAsync(); }
        var issue = DateTime.UtcNow.Date;
        var create = new CreateQuotationCommand($"EDIT-{Guid.NewGuid():N}", customer.Id, issue, issue.AddDays(10),
            "Inicial", "À vista", [new(null, "Original", "un", 100, 1, 0)], new(DepositType.Percentage, 20, "Entrada"));
        var response = await client.PostAsJsonAsync("/api/v1/quotations", create);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<Agendamento.Api.Features.Common.CreatedIdResponse>())!.Id;
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/notifications/send-quotation-notification", new { quotationId = id })).StatusCode);
        using (var scope = factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<INotificationService>().ProcessPendingAsync();
        using (var db = factory.Db(tenant)) Assert.Contains(await db.NotificationMessages.Where(n => n.CustomerId == customer.Id).ToListAsync(), n => n.Status == Agendamento.Api.Domain.Notifications.NotificationStatus.Sent);
        var edit = new UpdateQuotationCommand(create.Code + "-E", nextCustomer.Id, issue.AddDays(1), issue.AddDays(20),
            "Conteúdo revisado", "Parcelado", [new(null, "Revisado", "m2", 200, 3, 50)], new(DepositType.Percentage, 30, "Nova entrada"));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/v1/quotations/{id}", edit)).StatusCode);
        var saved = (await client.GetFromJsonAsync<QuotationDetailDto>($"/api/v1/quotations/{id}"))!;
        Assert.Equal(id, saved.Id); Assert.Equal(QuotationStatus.Pending, saved.Status);
        Assert.Equal(edit.Code, saved.Code); Assert.Equal(nextCustomer.Id, saved.CustomerId);
        Assert.Equal(edit.IssueDate, saved.IssueDate); Assert.Equal(edit.ValidUntil, saved.ValidUntil);
        Assert.Equal(edit.Notes, saved.Notes); Assert.Equal(edit.PaymentTerms, saved.PaymentTerms);
        Assert.Equal(600, saved.SubtotalAmount); Assert.Equal(50, saved.DiscountAmount); Assert.Equal(550, saved.TotalAmount);
        var item = Assert.Single(saved.Items); Assert.Equal("Revisado", item.ServiceName); Assert.Equal(3, item.Quantity);
        Assert.Equal(165, saved.DepositInfo!.RequiredAmount); Assert.Equal(385, saved.DepositInfo.RemainingBalance);
        Assert.Equal("Nova entrada", saved.DepositInfo.PaymentNotes);
        var snapshot = await client.GetStringAsync($"/api/v1/quotations/{id}");
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/v1/quotations/{id}", edit)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/v1/quotations/{id}", edit with { CustomerId = foreign.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/v1/quotations/{id}", edit with { Items = [] })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/v1/quotations/{id}", edit with { Deposit = new(DepositType.FixedAmount, 9999, null) })).StatusCode);
        Assert.Equal(snapshot, await client.GetStringAsync($"/api/v1/quotations/{id}"));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/v1/quotations/{id}", edit with { Deposit = null })).StatusCode);
        Assert.Null((await client.GetFromJsonAsync<QuotationDetailDto>($"/api/v1/quotations/{id}"))!.DepositInfo);
    }

    [Theory(DisplayName = "Estados finais bloqueiam edição sem alterar orçamento, itens ou sinal @spec:AC-206")]
    [InlineData(QuotationStatus.Approved)]
    [InlineData(QuotationStatus.Rejected)]
    [InlineData(QuotationStatus.Converted)]
    public Task FinalStateRejectsEdit(QuotationStatus status) => ProveFinalState(factory, status);

    internal static async Task ProveFinalState(TeamFinancialTestFactory factory, QuotationStatus status, Guid? tenantId = null)
    {
        var (client, _, tenant) = await factory.Member("owner_admin", [], tenantId);
        var customer = Customer.Create(tenant, "Cliente bloqueado fictício", "123", null, null, null);
        var quotation = Quotation.Create(tenant, $"FINAL-{Guid.NewGuid():N}", customer.Id, DateTime.UtcNow.Date, null, "Inicial", "Original");
        quotation.AddItem(QuotationItem.Create(null, "Original", "un", 100, 1, 0));
        quotation.SetDeposit(DepositType.Percentage, 20, "Entrada"); quotation.MarkAsPending();
        if (status == QuotationStatus.Rejected) quotation.Reject();
        else { quotation.Approve(); if (status == QuotationStatus.Converted) quotation.MarkAsConverted(); }
        using (var db = factory.Db(tenant)) { db.Customers.Add(customer); db.Quotations.Add(quotation); await db.SaveChangesAsync(); }
        var before = await client.GetStringAsync($"/api/v1/quotations/{quotation.Id}");
        var edit = new UpdateQuotationCommand("INVALID-EDIT", customer.Id, DateTime.UtcNow.Date, null, "Alterado", "Alterado",
            [new(null, "Alterado", "un", 999, 2, 0)], null);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/v1/quotations/{quotation.Id}", edit)).StatusCode);
        Assert.Equal(before, await client.GetStringAsync($"/api/v1/quotations/{quotation.Id}"));
        using (var db = factory.Db(tenant)) Assert.Empty(await db.ReceivableTitles.Where(r => r.QuotationId == quotation.Id).ToListAsync());
    }
}
