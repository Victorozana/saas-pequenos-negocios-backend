using Agendamento.Api.Domain.Quotations;
using Agendamento.IntegrationTests.Features;
using Xunit;
using static Agendamento.IntegrationTests.Tenancy.PostgreSqlRlsTests;
using System.Net;
using System.Net.Http.Json;
using Agendamento.Api.Application.Quotations.CreateQuotation;
using Agendamento.Api.Application.Quotations.GetQuotations;
using Agendamento.Api.Application.Quotations.UpdateQuotation;
using Agendamento.Api.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.IntegrationTests.Quotations;

public sealed class QuotationEditPostgreSqlTests
{
    [Fact(DisplayName = "Edição HTTP persiste itens e sinal em PostgreSQL exclusivo com isolamento RLS @spec:AC-206 @spec:AC-092")]
    public async Task PendingAndFinalStatesPersistUnderRls()
    {
        await using var database = await Database.Create();
        using var factory = new TeamFinancialTestFactory(database.RestrictedConnection, database.AdministrativeConnection);
        await QuotationEditHttpTests.ProvePendingEdit(factory, database.A, database.B);
        foreach (var status in new[] { QuotationStatus.Approved, QuotationStatus.Rejected, QuotationStatus.Converted })
            await QuotationEditHttpTests.ProveFinalState(factory, status, database.A);
    }

    [Fact(DisplayName = "Edição aguarda aprovação concorrente e rejeita conteúdo sem modificar estado final @spec:AC-206")]
    public async Task ConcurrentApprovalWinsWithoutStaleEdit()
    {
        await using var database = await Database.Create();
        using var factory = new TeamFinancialTestFactory(database.RestrictedConnection, database.AdministrativeConnection);
        var (client, _, tenant) = await factory.Member("owner_admin", [], database.A);
        var customer = Customer.Create(tenant, "Cliente concorrência fictício", "123", null, null, null);
        using (var seed = factory.Db(tenant)) { seed.Customers.Add(customer); await seed.SaveChangesAsync(); }
        var create = new CreateQuotationCommand($"RACE-{Guid.NewGuid():N}", customer.Id, DateTime.UtcNow.Date,
            null, "Original", "À vista", [new(null, "Original", "un", 100, 1, 0)], null);
        var response = await client.PostAsJsonAsync("/api/v1/quotations", create);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<Agendamento.Api.Features.Common.CreatedIdResponse>())!.Id;
        using var approvingDb = factory.Db(tenant);
        await using var transaction = await approvingDb.Database.BeginTransactionAsync();
        var quotation = await approvingDb.Quotations.SingleAsync(q => q.Id == id);
        quotation.Approve(); await approvingDb.SaveChangesAsync();
        var request = client.PutAsJsonAsync($"/api/v1/quotations/{id}", new UpdateQuotationCommand("STALE-EDIT", customer.Id,
            create.IssueDate, null, "Alterado", "Alterado", [new(null, "Alterado", "un", 999, 2, 0)], null));
        try
        {
            // Observe a real lock wait instead of assuming a timed delay proves concurrency.
            var waiting = false;
            for (var attempt = 0; attempt < 100 && !waiting; attempt++)
            {
                waiting = (long)(await Scalar(database.Admin,
                    "SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock' AND query LIKE '%Quotations%FOR UPDATE%'"))! > 0;
                if (!waiting) await Task.Delay(50);
            }
            Assert.True(waiting, "The edit must wait for the quotation row lock before reading its state.");
        }
        finally { await transaction.CommitAsync(); }
        Assert.Equal(HttpStatusCode.BadRequest, (await request).StatusCode);
        var saved = (await client.GetFromJsonAsync<QuotationDetailDto>($"/api/v1/quotations/{id}"))!;
        Assert.Equal(QuotationStatus.Approved, saved.Status); Assert.Equal(create.Code, saved.Code);
        Assert.Equal("Original", saved.Notes); Assert.Equal(100, saved.TotalAmount);
        Assert.Equal("Original", Assert.Single(saved.Items).ServiceName);
    }
}
