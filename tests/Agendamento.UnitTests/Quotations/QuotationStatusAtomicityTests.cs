using System.Text.Json;
using Agendamento.Api.Application.Financial.GenerateReceivablesFromQuotation;
using Agendamento.Api.Application.Quotations.UpdateQuotationStatus;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Agendamento.UnitTests.Quotations;

public class QuotationStatusAtomicityTests
{
    [Theory(DisplayName = "Decisão recusada de orçamento vencido preserva agregado rastreado e Save posterior @spec:AC-048")]
    [InlineData(QuotationStatus.Approved)]
    [InlineData(QuotationStatus.Rejected)]
    [InlineData(QuotationStatus.Pending)]
    [InlineData((QuotationStatus)999)]
    public async Task ExpiredPending_RefusedDecisionCannotLeakMutationThroughLaterSave(QuotationStatus requested)
    {
        await using var db = CreateContext();
        var quotation = ExpiredPending(db);
        await db.SaveChangesAsync();
        var before = JsonSerializer.Serialize(quotation);
        var handler = new UpdateQuotationStatusHandler(db, new GenerateReceivablesFromQuotationHandler(db));
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(quotation.Id, new(requested)));
        db.ChangeTracker.DetectChanges();
        Assert.Equal(EntityState.Unchanged, db.Entry(quotation).State);
        Assert.All(db.ChangeTracker.Entries(), entry => Assert.Equal(EntityState.Unchanged, entry.State));
        Assert.Equal(before, JsonSerializer.Serialize(quotation));
        Assert.Equal(0, await db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var persisted = await db.Quotations.Include(q => q.Items).SingleAsync();
        Assert.Equal(before, JsonSerializer.Serialize(persisted));
        Assert.Empty(await db.ReceivableTitles.ToListAsync());
    }

    [Fact(DisplayName = "Cancelamento de orçamento vencido persiste Canceled preservando conteúdo @spec:AC-048")]
    public async Task ExpiredPending_CancellationRemainsAllowed()
    {
        await using var db = CreateContext();
        var quotation = ExpiredPending(db);
        await db.SaveChangesAsync();
        var total = quotation.TotalAmount;
        var itemId = quotation.Items.Single().Id;
        var handler = new UpdateQuotationStatusHandler(db, new GenerateReceivablesFromQuotationHandler(db));
        Assert.True(await handler.HandleAsync(quotation.Id, new(QuotationStatus.Canceled)));
        db.ChangeTracker.Clear();
        var persisted = await db.Quotations.Include(q => q.Items).SingleAsync();
        Assert.Equal(QuotationStatus.Canceled, persisted.Status);
        Assert.Equal(total, persisted.TotalAmount);
        Assert.Equal(itemId, persisted.Items.Single().Id);
        Assert.Empty(await db.ReceivableTitles.ToListAsync());
    }

    private static AgendamentoDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AgendamentoDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new TenantContext());

    private static Quotation ExpiredPending(AgendamentoDbContext db)
    {
        var quotation = Quotation.Create(TenantContext.Id, "VENCIDO", Guid.NewGuid(), DateTime.UtcNow.Date.AddDays(-10), DateTime.UtcNow.Date.AddDays(-1), "Preservar", "Termos");
        quotation.AddItem(QuotationItem.Create(null, "Serviço", "un", 100m, 2m, 10m));
        quotation.SetDeposit(DepositType.Percentage, 20m, "Entrada");
        quotation.MarkAsPending();
        db.Quotations.Add(quotation);
        return quotation;
    }

    private sealed class TenantContext : ITenantContext
    {
        public static readonly Guid Id = Guid.NewGuid();
        public Guid TenantId => Id;
        public bool HasTenant => true;
    }
}
