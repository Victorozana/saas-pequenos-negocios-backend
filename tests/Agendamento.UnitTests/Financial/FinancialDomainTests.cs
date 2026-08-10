using Agendamento.Api.Domain.Financial;
using Xunit;

namespace Agendamento.UnitTests.Financial;

public class FinancialDomainTests
{
    [Fact(DisplayName = "Entidades financeiras devem pertencer a um Tenant @spec:AC-055")]
    public void FinancialEntities_ShouldImplement_ITenantOwned()
    {
        var tenantId = Guid.NewGuid();
        var receivable = ReceivableTitle.Create(tenantId, "Test", 100m, DateTime.UtcNow.AddDays(1));
        var payable = PayableTitle.Create(tenantId, "Supplier", "Test", 100m, DateTime.UtcNow.AddDays(1));

        Assert.Equal(tenantId, receivable.TenantId);
        Assert.Equal(tenantId, payable.TenantId);
        Assert.IsAssignableFrom<Agendamento.Domain.Common.ITenantOwned>(receivable);
        Assert.IsAssignableFrom<Agendamento.Domain.Common.ITenantOwned>(payable);
    }

    [Fact(DisplayName = "Pagamento parcial atualiza status para PartiallyPaid e abate o saldo @spec:AC-057")]
    public void RegisterPayment_ShouldUpdateStatusToPartiallyPaid_WhenPaymentIsLessThanBalance()
    {
        var title = ReceivableTitle.Create(Guid.NewGuid(), "Test", 100m, DateTime.UtcNow.AddDays(1));
        
        title.RegisterPayment(40m, PaymentMethod.Pix, DateTime.UtcNow);

        Assert.Equal(TransactionStatus.PartiallyPaid, title.Status);
        Assert.Equal(60m, title.BalanceDue);
        Assert.Equal(40m, title.PaidAmount);
    }

    [Fact(DisplayName = "Pagamento integral atualiza status para Paid e zera o saldo @spec:AC-057")]
    public void RegisterPayment_ShouldUpdateStatusToPaid_WhenPaymentEqualsBalance()
    {
        var title = PayableTitle.Create(Guid.NewGuid(), "Supplier", "Test", 100m, DateTime.UtcNow.AddDays(1));
        
        title.RegisterPayment(100m, PaymentMethod.BankTransfer, DateTime.UtcNow);

        Assert.Equal(TransactionStatus.Paid, title.Status);
        Assert.Equal(0m, title.BalanceDue);
        Assert.Equal(100m, title.PaidAmount);
    }

    [Fact(DisplayName = "Pagamento maior que o saldo lança exceção @spec:AC-057")]
    public void RegisterPayment_ShouldThrowException_WhenPaymentExceedsBalance()
    {
        var title = ReceivableTitle.Create(Guid.NewGuid(), "Test", 100m, DateTime.UtcNow.AddDays(1));
        
        Assert.Throws<InvalidOperationException>(() => 
            title.RegisterPayment(150m, PaymentMethod.Cash, DateTime.UtcNow));
    }

    [Fact(DisplayName = "Verificação de atraso marca título como Overdue se vencido @spec:AC-058")]
    public void CheckOverdue_ShouldUpdateStatusToOverdue_WhenDueDateIsPast()
    {
        var title = PayableTitle.Create(Guid.NewGuid(), "Supplier", "Test", 100m, DateTime.UtcNow.AddDays(-1));
        
        title.CheckOverdue();

        Assert.Equal(TransactionStatus.Overdue, title.Status);
    }

    [Fact(DisplayName = "Verificação de atraso não altera status se estiver no prazo @spec:AC-058")]
    public void CheckOverdue_ShouldNotUpdateStatus_WhenDueDateIsFuture()
    {
        var title = ReceivableTitle.Create(Guid.NewGuid(), "Test", 100m, DateTime.UtcNow.AddDays(1));
        
        title.CheckOverdue();

        Assert.Equal(TransactionStatus.Pending, title.Status);
    }
}
