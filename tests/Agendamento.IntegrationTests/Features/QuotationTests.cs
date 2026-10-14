using Agendamento.Api.Domain.Quotations;
using Xunit;
using Agendamento.Api.Application.Quotations.CreateQuotation;
using Agendamento.Api.Application.Quotations.UpdateQuotationStatus;
using Agendamento.Api.Application.WorkOrders.ConvertQuotationToWorkOrder;
using Microsoft.EntityFrameworkCore;
using Agendamento.Api.Infrastructure.Persistence;

namespace Agendamento.IntegrationTests.Features;

public class QuotationTests : IClassFixture<TeamFinancialTestFactory>, IAsyncLifetime
{
    private readonly TeamFinancialTestFactory _factory;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();

    public QuotationTests(TeamFinancialTestFactory factory)
    {
        _factory = factory;
    }

    private class TestTenantContext : Agendamento.Api.Application.Tenancy.ITenantContext
    {
        public TestTenantContext(Guid tenantId)
        {
            TenantId = tenantId;
            HasTenant = true;
        }

        public Guid TenantId { get; }
        public bool HasTenant { get; }
    }

    public async Task InitializeAsync()
    {
        using var db = GetDbContext(_tenantId);
        
        var customer = Agendamento.Api.Domain.Customers.Customer.Create(_tenantId, "Cliente Teste", "11999999999", null, null, null);
        typeof(Agendamento.Api.Domain.Customers.Customer).GetProperty("Id")!.SetValue(customer, _customerId);

        if (!db.Customers.Any(c => c.Id == _customerId))
        {
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
        }
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    private AgendamentoDbContext GetDbContext(Guid tenantId)
    {
        // The factory isolates its database and installs the same tenant write
        // guard for seeding and handler calls, including shadow item tenants.
        return _factory.Db(tenantId);
    }

    [Fact(DisplayName = "Criar orçamento com status Pendente e valor total calculado @spec:AC-090")]
    public async Task AC090_CreateQuotation_StatusPending_CalculatesTotal()
    {
        using var db = GetDbContext(_tenantId);
        var handler = new CreateQuotationHandler(db, new TestTenantContext(_tenantId));

        var command = new CreateQuotationCommand(
            "Q-001",
            _customerId,
            DateTime.UtcNow,
            null,
            null,
            null,
            new List<CreateQuotationItemDto>
            {
                new(null, "Serviço A", "Un", 50, 2, 0),
                new(null, "Serviço B", "Un", 100, 1, 0)
            },
            null
        );

        var id = await handler.HandleAsync(command);

        using var persisted = GetDbContext(_tenantId);
        var quotation = await persisted.Quotations.Include(q => q.Items).FirstOrDefaultAsync(q => q.Id == id);
        
        Assert.NotNull(quotation);
        Assert.Equal(QuotationStatus.Pending, quotation.Status);
        Assert.Equal(200, quotation.TotalAmount); // (2*50) + (1*100)
        Assert.Equal(_tenantId, quotation.TenantId);
        Assert.Equal(_customerId, quotation.CustomerId);
        Assert.Equal(2, quotation.Items.Count);
        Assert.All(quotation.Items, item => Assert.Equal(_tenantId, persisted.Entry(item).Property<Guid>("TenantId").CurrentValue));
    }

    [Fact(DisplayName = "Somente orçamentos aprovados podem ser convertidos em OS @spec:AC-091")]
    public async Task AC091_ConvertApprovedQuotationToWorkOrder()
    {
        using var db = GetDbContext(_tenantId);
        var createHandler = new CreateQuotationHandler(db, new TestTenantContext(_tenantId));
        var updateHandler = new UpdateQuotationStatusHandler(db, new Agendamento.Api.Application.Financial.GenerateReceivablesFromQuotation.GenerateReceivablesFromQuotationHandler(db));
        
        // Handler and unit of work must share the exact same context.
        var uow = new UnitOfWork(db);
        var convertHandler = new ConvertQuotationToWorkOrderHandler(db, new TestTenantContext(_tenantId), uow);

        var command = new CreateQuotationCommand(
            "Q-002",
            _customerId,
            DateTime.UtcNow,
            null,
            null,
            null,
            new List<CreateQuotationItemDto> { new(null, "Serviço", "Un", 10, 1, 0) },
            null
        );
        var id = await createHandler.HandleAsync(command);

        // Tenta converter pendente (deve falhar)
        await Assert.ThrowsAsync<InvalidOperationException>(() => convertHandler.HandleAsync(new ConvertQuotationToWorkOrderCommand { QuotationId = id }));

        // Aprova
        await updateHandler.HandleAsync(id, new UpdateQuotationStatusCommand(QuotationStatus.Approved));

        // Tenta converter aprovado (deve sucesso)
        var workOrderId = await convertHandler.HandleAsync(new ConvertQuotationToWorkOrderCommand { QuotationId = id });
        Assert.NotEqual(Guid.Empty, workOrderId);
        Assert.Equal(workOrderId, await convertHandler.HandleAsync(new ConvertQuotationToWorkOrderCommand { QuotationId = id }));
        using var persisted = GetDbContext(_tenantId);
        var order = await persisted.WorkOrders.Include(w => w.Items).SingleAsync(w => w.QuotationId == id);
        Assert.Equal(workOrderId, order.Id);
        Assert.Equal(_tenantId, order.TenantId);
        Assert.Equal(_customerId, order.CustomerId);
        Assert.Equal(Agendamento.Api.Domain.WorkOrders.WorkOrderStatus.Scheduled, order.Status);
        Assert.Single(order.Items);
        Assert.Equal(QuotationStatus.Converted, (await persisted.Quotations.SingleAsync(q => q.Id == id)).Status);
    }

    [Fact(DisplayName = "Orçamentos não podem ser acessados por outro tenant @spec:AC-092")]
    public async Task AC092_TenantIsolation()
    {
        using var db1 = GetDbContext(_tenantId);
        var createHandler = new CreateQuotationHandler(db1, new TestTenantContext(_tenantId));

        var command = new CreateQuotationCommand(
            "Q-003",
            _customerId,
            DateTime.UtcNow,
            null,
            null,
            null,
            new List<CreateQuotationItemDto> { new(null, "Serviço", "Un", 10, 1, 0) },
            null
        );
        var id = await createHandler.HandleAsync(command);

        // Loga como outro tenant
        using var db2 = GetDbContext(Guid.NewGuid());
        var quotation = await db2.Quotations.FirstOrDefaultAsync(q => q.Id == id);
        
        Assert.Null(quotation);
        Assert.Empty(await db2.Set<QuotationItem>().ToListAsync());
        using var own = GetDbContext(_tenantId);
        Assert.Single(await own.Set<QuotationItem>().ToListAsync());
        using var isolatedFactory = new TeamFinancialTestFactory();
        using var isolated = isolatedFactory.Db(_tenantId);
        Assert.False(await isolated.Quotations.AnyAsync(q => q.Id == id));
        Assert.Empty(await isolated.Set<QuotationItem>().ToListAsync());
    }

    [Fact(DisplayName = "Rejeição de orçamento convertido é recusada sem mutação rastreada @spec:AC-093")]
    public async Task AC093_CannotUpdateStatusOfConvertedQuotation()
    {
        using var db = GetDbContext(_tenantId);
        var createHandler = new CreateQuotationHandler(db, new TestTenantContext(_tenantId));
        var updateHandler = new UpdateQuotationStatusHandler(db, new Agendamento.Api.Application.Financial.GenerateReceivablesFromQuotation.GenerateReceivablesFromQuotationHandler(db));
        
        var uow = new UnitOfWork(db);
        var convertHandler = new ConvertQuotationToWorkOrderHandler(db, new TestTenantContext(_tenantId), uow);

        var command = new CreateQuotationCommand(
            "Q-004",
            _customerId,
            DateTime.UtcNow,
            null,
            null,
            null,
            new List<CreateQuotationItemDto> { new(null, "Serviço", "Un", 10, 1, 0) },
            null
        );
        var id = await createHandler.HandleAsync(command);

        // Aprova e converte
        await updateHandler.HandleAsync(id, new UpdateQuotationStatusCommand(QuotationStatus.Approved));
        await convertHandler.HandleAsync(new ConvertQuotationToWorkOrderCommand { QuotationId = id });

        // Act - Tenta rejeitar
        await Assert.ThrowsAsync<InvalidOperationException>(() => updateHandler.HandleAsync(id, new UpdateQuotationStatusCommand(QuotationStatus.Rejected)));
        Assert.All(db.ChangeTracker.Entries(), entry => Assert.Equal(EntityState.Unchanged, entry.State));
        Assert.Equal(0, await db.SaveChangesAsync());
        using var persisted = GetDbContext(_tenantId);
        Assert.Equal(QuotationStatus.Converted, (await persisted.Quotations.SingleAsync(q => q.Id == id)).Status);
        Assert.Single(await persisted.WorkOrders.Where(w => w.QuotationId == id).ToListAsync());
    }

    [Theory(DisplayName = "Decisões finais e aprovação idempotente preservam tracker e gravação posterior @spec:AC-093")]
    [InlineData(QuotationStatus.Approved, QuotationStatus.Approved, true)]
    [InlineData(QuotationStatus.Converted, QuotationStatus.Approved, true)]
    [InlineData(QuotationStatus.Approved, QuotationStatus.Rejected, false)]
    [InlineData(QuotationStatus.Converted, QuotationStatus.Rejected, false)]
    [InlineData(QuotationStatus.Rejected, QuotationStatus.Approved, false)]
    [InlineData(QuotationStatus.Rejected, QuotationStatus.Rejected, false)]
    public async Task FinalDecisionsPreserveTrackedGraph(QuotationStatus initial, QuotationStatus requested, bool succeeds)
    {
        Guid id;
        using (var seed = GetDbContext(_tenantId))
        {
            id = await new CreateQuotationHandler(seed, new TestTenantContext(_tenantId)).HandleAsync(new(
                "Q-FINAL", _customerId, DateTime.UtcNow, null, "Conteúdo original", "Termos originais",
                [new(null, "Serviço", "un", 0.03m, 1m, 0m)], null));
            var status = new UpdateQuotationStatusHandler(seed, new Agendamento.Api.Application.Financial.GenerateReceivablesFromQuotation.GenerateReceivablesFromQuotationHandler(seed));
            await status.HandleAsync(id, new(initial == QuotationStatus.Rejected ? QuotationStatus.Rejected : QuotationStatus.Approved));
            if (initial == QuotationStatus.Converted)
                await new ConvertQuotationToWorkOrderHandler(seed, new TestTenantContext(_tenantId), new UnitOfWork(seed)).HandleAsync(new() { QuotationId = id });
        }
        using var db = GetDbContext(_tenantId);
        await db.Quotations.Include(q => q.Items).SingleAsync(q => q.Id == id);
        await db.ReceivableTitles.Where(r => r.QuotationId == id).LoadAsync();
        await db.WorkOrders.Include(w => w.Items).Where(w => w.QuotationId == id).LoadAsync();
        var entries = db.ChangeTracker.Entries().ToArray();
        var before = entries.Select(e => e.CurrentValues.Clone()).ToArray();
        var handler = new UpdateQuotationStatusHandler(db, new Agendamento.Api.Application.Financial.GenerateReceivablesFromQuotation.GenerateReceivablesFromQuotationHandler(db));
        if (succeeds) await handler.HandleAsync(id, new(requested));
        else await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(id, new(requested)));
        Assert.Equal(entries.Length, db.ChangeTracker.Entries().Count());
        for (var index = 0; index < entries.Length; index++)
        {
            Assert.Equal(EntityState.Unchanged, entries[index].State);
            foreach (var property in before[index].Properties)
                Assert.Equal(before[index][property], entries[index].CurrentValues[property]);
        }
        Assert.Equal(0, await db.SaveChangesAsync());
        using var persisted = GetDbContext(_tenantId);
        Assert.Equal(initial, (await persisted.Quotations.SingleAsync(q => q.Id == id)).Status);
        Assert.Equal(initial == QuotationStatus.Converted ? 1 : 0, await persisted.WorkOrders.CountAsync(w => w.QuotationId == id));
        Assert.Equal(initial == QuotationStatus.Rejected ? 0 : 1, await persisted.ReceivableTitles.CountAsync(r => r.QuotationId == id));
    }
}
