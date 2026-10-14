using System.Text.Json;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Application.WorkOrders.ConvertQuotationToWorkOrder;
using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Domain.WorkOrders;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Agendamento.UnitTests.Quotations;

public sealed class QuotationConversionAtomicityTests
{
    [Fact(DisplayName = "Itens de OS conservam precisão teto e desconto contra subtotal arredondado @spec:AC-091 @spec:AC-046")]
    public void WorkOrderItemsRespectQuotationMoneyRules()
    {
        foreach (var values in new[] {
            (1.001m, 1m, 0m), (1m, 1.001m, 0m), (1m, 1m, 0.001m),
            (10000000000000000m, 1m, 0m), (1m, 10000000000000000m, 0m),
            (1m, 1m, 1.01m), (9999999999999999.99m, 2m, 0m) })
            Assert.Throws<ArgumentException>(() => WorkOrderItem.Create(null, "service", "un", values.Item2, values.Item1, values.Item3));
        var rounded = WorkOrderItem.Create(null, "service", "un", 0.5m, 0.03m, 0.02m);
        Assert.Equal(0m, Agendamento.Api.Application.WorkOrders.GetWorkOrderById.WorkOrderItemResponse.FromEntity(rounded).TotalAmount);
        var free = WorkOrderItem.Create(null, "free", "un", 9999999999999999.99m, 0m, 0m);
        Assert.Equal(9999999999999999.99m, free.Quantity);
        var maximum = WorkOrderItem.Create(null, "max", "un", 1m, 9999999999999999.99m, 0m);
        Assert.Equal(9999999999999999.99m, Agendamento.Api.Application.WorkOrders.GetWorkOrderById.WorkOrderItemResponse.FromEntity(maximum).TotalAmount);
    }

    private sealed class Tenant(Guid id) : ITenantContext
    {
        public Guid TenantId => id;
        public bool HasTenant => id != Guid.Empty;
    }

    [Theory(DisplayName = "Conversão recusada preserva agregado rastreado e gravação posterior @spec:AC-091 @spec:AC-093")]
    [InlineData(QuotationStatus.Draft)]
    [InlineData(QuotationStatus.Pending)]
    [InlineData(QuotationStatus.Rejected)]
    [InlineData(QuotationStatus.Expired)]
    [InlineData(QuotationStatus.Canceled)]
    [InlineData(QuotationStatus.Converted)]
    public async Task RefusedConversionDoesNotStageAnyMutation(QuotationStatus status)
    {
        var tenant = new Tenant(Guid.NewGuid());
        await using var db = new AgendamentoDbContext(new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new TenantSaveChangesInterceptor(tenant)).Options, tenant);
        var quotation = Quotation.Create(tenant.TenantId, "preserve", Guid.NewGuid(), DateTime.UtcNow.Date.AddDays(-2),
            status == QuotationStatus.Expired ? DateTime.UtcNow.Date.AddDays(-1) : null, "notes", "terms");
        quotation.AddItem(QuotationItem.Create(null, "service", "un", 12.34m, 2.5m, 0.05m));
        quotation.SetDeposit(DepositType.Percentage, 25m, "deposit");
        if (status != QuotationStatus.Draft) quotation.MarkAsPending();
        if (status == QuotationStatus.Rejected) quotation.Reject();
        if (status == QuotationStatus.Expired) quotation.CheckExpiration();
        if (status == QuotationStatus.Canceled) quotation.Cancel();
        if (status == QuotationStatus.Converted) { quotation.Approve(); quotation.MarkAsConverted(); }
        db.Add(quotation); await db.SaveChangesAsync();
        var before = JsonSerializer.Serialize(quotation);
        var entries = db.ChangeTracker.Entries().Select(e => e.Entity).ToHashSet();
        var handler = new ConvertQuotationToWorkOrderHandler(db, tenant, new UnitOfWork(db));
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(new() { QuotationId = quotation.Id }));
        db.ChangeTracker.DetectChanges();
        Assert.Equal(before, JsonSerializer.Serialize(quotation));
        Assert.True(entries.SetEquals(db.ChangeTracker.Entries().Select(e => e.Entity)));
        Assert.All(db.ChangeTracker.Entries(), e => Assert.Equal(EntityState.Unchanged, e.State));
        Assert.Equal(0, await db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        Assert.Equal(before, JsonSerializer.Serialize(await db.Quotations.Include(q => q.Items).SingleAsync()));
        Assert.Empty(await db.WorkOrders.ToListAsync());
        Assert.Empty(await db.ReceivableTitles.ToListAsync());
    }

    [Fact(DisplayName = "Conversão sem identidade não acessa nem modifica orçamento @spec:AC-092")]
    public async Task MissingTenantCannotConvert()
    {
        var tenant = new Tenant(Guid.Empty);
        await using var db = new AgendamentoDbContext(new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenant);
        var handler = new ConvertQuotationToWorkOrderHandler(db, tenant, new UnitOfWork(db));
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(new() { QuotationId = Guid.NewGuid() }));
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Equal(0, await db.SaveChangesAsync());
    }
}
