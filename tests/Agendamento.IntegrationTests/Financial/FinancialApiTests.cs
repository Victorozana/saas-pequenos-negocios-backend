using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agendamento.Api.Application.Financial.CreatePayable;
using Agendamento.Api.Application.Financial.GetFinancialStatement;
using Agendamento.Api.Application.Financial.RegisterPayment;
using Agendamento.Api.Application.Financial.GenerateReceivablesFromQuotation;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Customers;
using Agendamento.Api.Domain.Financial;
using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Agendamento.IntegrationTests.Financial;

public class FinancialApiTests
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

    [Fact(DisplayName = "Orçamento aprovado com sinal gera títulos a receber correspondentes @spec:AC-056")]
    public async Task GenerateReceivablesFromQuotation_ShouldCreateDepositAndRemainingTitles()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var db = GetDbContext(tenantId, dbName);

        var customer = Customer.Create(tenantId, "Customer 1", "123", null, null, null);
        var quotation = Quotation.Create(tenantId, "Q1", customer.Id, DateTime.UtcNow, null, null, null);
        quotation.AddItem(QuotationItem.Create(Guid.NewGuid(), "Service", "un", 500m, 1, 0));
        quotation.SetDeposit(DepositType.FixedAmount, 200m, "Sinal fixo");
        quotation.MarkAsPending();
        quotation.Approve();

        db.Customers.Add(customer);
        db.Quotations.Add(quotation);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var handler = new GenerateReceivablesFromQuotationHandler(db);
        await handler.HandleAsync(quotation, CancellationToken.None);

        var receivables = await db.ReceivableTitles.ToListAsync();

        Assert.Equal(2, receivables.Count);
        
        var depositTitle = receivables.Single(r => r.Description.Contains("Sinal/Entrada"));
        Assert.Equal(200m, depositTitle.OriginalAmount);
        
        var remainingTitle = receivables.Single(r => r.Description.Contains("Saldo"));
        Assert.Equal(300m, remainingTitle.OriginalAmount);
    }

    [Fact(DisplayName = "Permite criar Contas a Pagar e Registrar Pagamento @spec:AC-057")]
    public async Task Can_Create_Payable_And_Register_Payment()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var db = GetDbContext(tenantId, dbName);
        var tenantContext = new DummyTenantContext(tenantId);

        var createHandler = new CreatePayableHandler(db, tenantContext);
        var command = new CreatePayableCommand("Supplier A", "Energy Bill", 150.50m, DateTime.UtcNow.AddDays(10));
        var payableId = await createHandler.HandleAsync(command, CancellationToken.None);

        var dbAct = GetDbContext(tenantId, dbName);
        var payHandler = new Agendamento.Api.Application.Financial.RegisterPayment.RegisterPayablePaymentHandler(dbAct);
        var payCommand = new Agendamento.Api.Application.Financial.RegisterPayment.RegisterPaymentCommand(150.50m, PaymentMethod.Pix, DateTime.UtcNow, "Paid");
        var result = await payHandler.HandleAsync(payableId, payCommand, CancellationToken.None);

        Assert.True(result);

        var dbAssert = GetDbContext(tenantId, dbName);
        var title = await dbAssert.PayableTitles.FindAsync(payableId);
        Assert.NotNull(title);
        Assert.Equal(TransactionStatus.Paid, title.Status);
        Assert.Equal(0, title.BalanceDue);
    }

    [Fact(DisplayName = "Consulta de extrato retorna somatórios corretos do período @spec:AC-059")]
    public async Task GetFinancialStatement_ShouldReturnCorrectSums()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var db = GetDbContext(tenantId, dbName);

        var rec1 = ReceivableTitle.Create(tenantId, "Rec 1", 1000m, DateTime.UtcNow); // paid
        var rec2 = ReceivableTitle.Create(tenantId, "Rec 2", 500m, DateTime.UtcNow);  // pending
        
        var pay1 = PayableTitle.Create(tenantId, "Sup 1", "Pay 1", 300m, DateTime.UtcNow); // paid
        var pay2 = PayableTitle.Create(tenantId, "Sup 2", "Pay 2", 200m, DateTime.UtcNow); // pending

        db.ReceivableTitles.AddRange(rec1, rec2);
        db.PayableTitles.AddRange(pay1, pay2);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var db2 = GetDbContext(tenantId, dbName);
        var rec1Db = await db2.ReceivableTitles.FindAsync(rec1.Id);
        rec1Db!.RegisterPayment(1000m, PaymentMethod.Pix, DateTime.UtcNow);
        db2.Entry(rec1Db).Collection(t => t.Payments).FindEntry(rec1Db.Payments.Last())!.State = EntityState.Added;

        var pay1Db = await db2.PayableTitles.FindAsync(pay1.Id);
        pay1Db!.RegisterPayment(300m, PaymentMethod.BankTransfer, DateTime.UtcNow);
        db2.Entry(pay1Db).Collection(t => t.Payments).FindEntry(pay1Db.Payments.Last())!.State = EntityState.Added;
        
        await db2.SaveChangesAsync();

        var db3 = GetDbContext(tenantId, dbName);
        var handler = new GetFinancialStatementHandler(db3);
        var statement = await handler.HandleAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), CancellationToken.None);

        Assert.Equal(1500m, statement.TotalReceivables);
        Assert.Equal(500m, statement.TotalPayables);
        Assert.Equal(1000m, statement.TotalPaidReceivables);
        Assert.Equal(300m, statement.TotalPaidPayables);
        Assert.Equal(1000m, statement.ProjectedBalance); // 1500 - 500
        Assert.Equal(700m, statement.RealizedBalance);   // 1000 - 300
    }
}
