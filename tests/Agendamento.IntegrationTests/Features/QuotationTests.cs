using Agendamento.Api.Domain.Quotations;
using Agendamento.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Agendamento.Api.Application.Quotations.CreateQuotation;
using Agendamento.Api.Application.Quotations.UpdateQuotationStatus;
using Agendamento.Api.Application.WorkOrders.ConvertQuotationToWorkOrder;
using Microsoft.EntityFrameworkCore;
using Agendamento.Api.Application.Common;
using Agendamento.Api.Infrastructure.Persistence;

namespace Agendamento.IntegrationTests.Features;

public class QuotationTests : IClassFixture<AgendamentoApiFactory>, IAsyncLifetime
{
    private readonly AgendamentoApiFactory _factory;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();

    public QuotationTests(AgendamentoApiFactory factory)
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
        var options = new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseInMemoryDatabase("IntegrationTestDb")
            .Options;

        using var db = new AgendamentoDbContext(options, new TestTenantContext(_tenantId));
        
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
        var options = new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseInMemoryDatabase("IntegrationTestDb")
            .Options;
        return new AgendamentoDbContext(options, new TestTenantContext(tenantId));
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

        var quotation = await db.Quotations.Include(q => q.Items).FirstOrDefaultAsync(q => q.Id == id);
        
        Assert.NotNull(quotation);
        Assert.Equal(QuotationStatus.Pending, quotation.Status);
        Assert.Equal(200, quotation.TotalAmount); // (2*50) + (1*100)
    }

    [Fact(DisplayName = "Somente orçamentos aprovados podem ser convertidos em OS @spec:AC-091")]
    public async Task AC091_ConvertApprovedQuotationToWorkOrder()
    {
        using var db = GetDbContext(_tenantId);
        var createHandler = new CreateQuotationHandler(db, new TestTenantContext(_tenantId));
        var updateHandler = new UpdateQuotationStatusHandler(db, new Agendamento.Api.Application.Financial.GenerateReceivablesFromQuotation.GenerateReceivablesFromQuotationHandler(db));
        
        // Mock unit of work
        var scope = _factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
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
    }

    [Fact(DisplayName = "Não é possível aprovar/rejeitar orçamento já convertido @spec:AC-093")]
    public async Task AC093_CannotUpdateStatusOfConvertedQuotation()
    {
        using var db = GetDbContext(_tenantId);
        var createHandler = new CreateQuotationHandler(db, new TestTenantContext(_tenantId));
        var updateHandler = new UpdateQuotationStatusHandler(db, new Agendamento.Api.Application.Financial.GenerateReceivablesFromQuotation.GenerateReceivablesFromQuotationHandler(db));
        
        var scope = _factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
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
    }
}
