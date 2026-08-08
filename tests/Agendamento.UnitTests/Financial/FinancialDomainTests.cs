using Agendamento.Api.Domain.Financial;
using Xunit;

namespace Agendamento.UnitTests.Financial;

public class FinancialDomainTests
{
    [Fact]
    public void RegisterPayment_ShouldUpdateStatusToPartiallyPaid_WhenPaymentIsLessThanBalance()
    {
        var title = ReceivableTitle.Create(Guid.NewGuid(), "Test", 100m, DateTime.UtcNow.AddDays(1));
        
        title.RegisterPayment(40m, PaymentMethod.Pix, DateTime.UtcNow);

        Assert.Equal(TransactionStatus.PartiallyPaid, title.Status);
        Assert.Equal(60m, title.BalanceDue);
        Assert.Equal(40m, title.PaidAmount);
    }

    [Fact]
    public void RegisterPayment_ShouldUpdateStatusToPaid_WhenPaymentEqualsBalance()
    {
        var title = PayableTitle.Create(Guid.NewGuid(), "Supplier", "Test", 100m, DateTime.UtcNow.AddDays(1));
        
        title.RegisterPayment(100m, PaymentMethod.BankTransfer, DateTime.UtcNow);

        Assert.Equal(TransactionStatus.Paid, title.Status);
        Assert.Equal(0m, title.BalanceDue);
        Assert.Equal(100m, title.PaidAmount);
    }

    [Fact]
    public void RegisterPayment_ShouldThrowException_WhenPaymentExceedsBalance()
    {
        var title = ReceivableTitle.Create(Guid.NewGuid(), "Test", 100m, DateTime.UtcNow.AddDays(1));
        
        Assert.Throws<InvalidOperationException>(() => 
            title.RegisterPayment(150m, PaymentMethod.Cash, DateTime.UtcNow));
    }

    [Fact]
    public void CheckOverdue_ShouldUpdateStatusToOverdue_WhenDueDateIsPast()
    {
        var title = PayableTitle.Create(Guid.NewGuid(), "Supplier", "Test", 100m, DateTime.UtcNow.AddDays(-1));
        
        title.CheckOverdue();

        Assert.Equal(TransactionStatus.Overdue, title.Status);
    }

    [Fact]
    public void CheckOverdue_ShouldNotUpdateStatus_WhenDueDateIsFuture()
    {
        var title = ReceivableTitle.Create(Guid.NewGuid(), "Test", 100m, DateTime.UtcNow.AddDays(1));
        
        title.CheckOverdue();

        Assert.Equal(TransactionStatus.Pending, title.Status);
    }
}
