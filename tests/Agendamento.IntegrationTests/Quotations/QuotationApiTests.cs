using System;
using System.Linq;
using System.Threading.Tasks;
using Agendamento.Api.Application.Quotations.CreateQuotation;
using Agendamento.Api.Application.Quotations.ExportPdf;
using Agendamento.Api.Application.Quotations.GetQuotations;
using Agendamento.Api.Application.Quotations.UpdateQuotationStatus;
using Agendamento.Api.Application.Services.CreateService;
using Agendamento.Api.Application.Services.GetServices;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Customers;
using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Infrastructure.Pdf;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Agendamento.IntegrationTests.Quotations;

public class QuotationApiTests
{
    private class DummyTenantContext : ITenantContext
    {
        public DummyTenantContext(Guid tenantId)
        {
            TenantId = tenantId;
            HasTenant = true;
        }

        public Guid TenantId { get; }
        public bool HasTenant { get; }
    }

    private AgendamentoDbContext GetDbContext(Guid tenantId, string dbName)
    {
        var options = new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var context = new AgendamentoDbContext(options, new DummyTenantContext(tenantId));
        context.Database.EnsureCreated();
        return context;
    }

    [Fact(DisplayName = "Itens do catálogo de serviços e orçamentos pertencem obrigatoriamente ao TenantId @spec:AC-045")]
    public async Task ServiceItems_And_Quotations_MustBelongToTenant()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var db = GetDbContext(tenantId, dbName);

        var customer = Customer.Create(tenantId, "Cliente Teste AC45", "11999999999", "teste45@email.com", null, null);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var createServiceHandler = new CreateServiceHandler(db, new DummyTenantContext(tenantId));
        var serviceId = await createServiceHandler.HandleAsync(new CreateServiceCommand("Instalação", "Serviço de instalação", "un", 200m));

        var createQuotationHandler = new CreateQuotationHandler(db, new DummyTenantContext(tenantId));
        var quotationId = await createQuotationHandler.HandleAsync(new CreateQuotationCommand(
            "ORC-AC45",
            customer.Id,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(10),
            "Nota",
            "À vista",
            new() { new CreateQuotationItemDto(serviceId, "Instalação", "un", 200m, 1, 0) },
            new CreateDepositDto(DepositType.Percentage, 30m, "Entrada 30%")
        ));

        var serviceInDb = await db.ServiceItems.FirstOrDefaultAsync(s => s.Id == serviceId);
        var quotationInDb = await db.Quotations.FirstOrDefaultAsync(q => q.Id == quotationId);

        Assert.NotNull(serviceInDb);
        Assert.Equal(tenantId, serviceInDb.TenantId);

        Assert.NotNull(quotationInDb);
        Assert.Equal(tenantId, quotationInDb.TenantId);
    }

    [Fact(DisplayName = "Criação de orçamento calcula subtotais, descontos, total e sinal @spec:AC-046")]
    public async Task Post_Quotations_CreatesQuotationAndCalculatesDeposit()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var db = GetDbContext(tenantId, dbName);

        var customer = Customer.Create(tenantId, "Cliente AC46", "11988888888", "cliente46@email.com", null, null);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var createQuotationHandler = new CreateQuotationHandler(db, new DummyTenantContext(tenantId));
        var quotationId = await createQuotationHandler.HandleAsync(new CreateQuotationCommand(
            "ORC-AC46",
            customer.Id,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(15),
            "Orçamento com sinal",
            "50% no aceite e 50% na entrega",
            new() 
            { 
                new CreateQuotationItemDto(null, "Serviço A", "un", 500m, 2, 50m), // Subtotal 1000, Total 950
                new CreateQuotationItemDto(null, "Serviço B", "m2", 100m, 5, 0m)   // Subtotal 500, Total 500
            },
            new CreateDepositDto(DepositType.Percentage, 50m, "50% de sinal")
        ));

        var getQuotationHandler = new GetQuotationByIdHandler(db);
        var quotation = await getQuotationHandler.HandleAsync(quotationId);

        Assert.NotNull(quotation);
        Assert.Equal(1500m, quotation.SubtotalAmount);
        Assert.Equal(50m, quotation.DiscountAmount);
        Assert.Equal(1450m, quotation.TotalAmount);
        Assert.NotNull(quotation.DepositInfo);
        Assert.Equal(50m, quotation.DepositInfo.Value);
        Assert.Equal(725m, quotation.DepositInfo.RequiredAmount);
        Assert.Equal(725m, quotation.DepositInfo.RemainingBalance);
    }

    [Fact(DisplayName = "Exportação de orçamento em PDF gera bytes válidos em stream application/pdf @spec:AC-047")]
    public async Task Get_QuotationPdf_ReturnsApplicationPdfStream()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var db = GetDbContext(tenantId, dbName);

        var customer = Customer.Create(tenantId, "Cliente PDF AC47", "11977777777", null, null, null);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var createQuotationHandler = new CreateQuotationHandler(db, new DummyTenantContext(tenantId));
        var quotationId = await createQuotationHandler.HandleAsync(new CreateQuotationCommand(
            "ORC-PDF-47",
            customer.Id,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(30),
            "Observações de teste em PDF",
            "Sinal 40%",
            new() { new CreateQuotationItemDto(null, "Marmore Travertino", "m2", 350m, 4, 100m) },
            new CreateDepositDto(DepositType.FixedAmount, 500m, "Sinal fixo de 500")
        ));

        var getQuotationHandler = new GetQuotationByIdHandler(db);
        var quotation = await getQuotationHandler.HandleAsync(quotationId);
        Assert.NotNull(quotation);

        IQuotationPdfGenerator pdfGenerator = new QuestPdfQuotationGenerator();
        var pdfBytes = await pdfGenerator.GeneratePdfAsync(quotation);

        Assert.NotNull(pdfBytes);
        Assert.NotEmpty(pdfBytes);
        // Validar assinatura PDF (%PDF-)
        var header = System.Text.Encoding.ASCII.GetString(pdfBytes.Take(4).ToArray());
        Assert.Equal("%PDF", header);
    }

    [Fact(DisplayName = "Validação de transições de status do orçamento @spec:AC-048")]
    public async Task Quotation_StatusTransitions_ShouldWorkCorrectly()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var db = GetDbContext(tenantId, dbName);

        var customer = Customer.Create(tenantId, "Cliente Status AC48", "11966666666", null, null, null);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var createQuotationHandler = new CreateQuotationHandler(db, new DummyTenantContext(tenantId));
        var quotationId = await createQuotationHandler.HandleAsync(new CreateQuotationCommand(
            "ORC-ST-48",
            customer.Id,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(5),
            null,
            null,
            new() { new CreateQuotationItemDto(null, "Portão Automático", "un", 1200m, 1, 0m) },
            null
        ));

        var updateStatusHandler = new UpdateQuotationStatusHandler(db, null!);
        
        // Transição Draft -> Pending
        var pendingSuccess = await updateStatusHandler.HandleAsync(quotationId, new UpdateQuotationStatusCommand(QuotationStatus.Pending));
        Assert.True(pendingSuccess);

        var getQuotationHandler = new GetQuotationByIdHandler(db);
        var qPending = await getQuotationHandler.HandleAsync(quotationId);
        Assert.NotNull(qPending);
        Assert.Equal(QuotationStatus.Pending, qPending.Status);
    }

    [Fact(DisplayName = "Isolamento multi-tenant impede que um tenant acesse orçamento ou serviço de outro tenant @spec:AC-049 @principle:P-004")]
    public async Task MultiTenant_Isolation_ReturnsNotFound_ForOtherTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        // Registrar dados no Tenant A
        var dbA = GetDbContext(tenantA, dbName);
        var customerA = Customer.Create(tenantA, "Cliente Tenant A", "11955555555", null, null, null);
        dbA.Customers.Add(customerA);
        await dbA.SaveChangesAsync();

        var createServiceHandlerA = new CreateServiceHandler(dbA, new DummyTenantContext(tenantA));
        var serviceIdA = await createServiceHandlerA.HandleAsync(new CreateServiceCommand("Serviço A", "Desc", "un", 100m));

        var createQuotationHandlerA = new CreateQuotationHandler(dbA, new DummyTenantContext(tenantA));
        var quotationIdA = await createQuotationHandlerA.HandleAsync(new CreateQuotationCommand(
            "ORC-TENANT-A",
            customerA.Id,
            DateTime.UtcNow,
            null,
            null,
            null,
            new() { new CreateQuotationItemDto(serviceIdA, "Serviço A", "un", 100m, 1, 0) },
            null
        ));

        // Tentar consultar a partir do contexto do Tenant B
        var dbB = GetDbContext(tenantB, dbName);
        var getServiceHandlerB = new GetServiceByIdHandler(dbB);
        var serviceTenantB = await getServiceHandlerB.HandleAsync(serviceIdA);
        Assert.Null(serviceTenantB);

        var getQuotationHandlerB = new GetQuotationByIdHandler(dbB);
        var quotationTenantB = await getQuotationHandlerB.HandleAsync(quotationIdA);
        Assert.Null(quotationTenantB);
    }
}
