using Agendamento.Api.Domain.Quotations;
using Xunit;

namespace Agendamento.UnitTests.Quotations;

public class QuotationDomainTests
{
    [Fact]
    public void AddItem_ShouldRecalculateTotals()
    {
        // Arrange
        var quotation = Quotation.Create(Guid.NewGuid(), "ORC-001", Guid.NewGuid(), DateTime.UtcNow, null, null, null);
        var item = QuotationItem.Create(null, "Test Service", "un", 100m, 2, 50m); // 200 - 50 = 150

        // Act
        quotation.AddItem(item);

        // Assert
        Assert.Equal(200m, quotation.SubtotalAmount);
        Assert.Equal(50m, quotation.DiscountAmount);
        Assert.Equal(150m, quotation.TotalAmount);
    }

    [Fact]
    public void SetDeposit_Percentage_ShouldCalculateRequiredAmount()
    {
        // Arrange
        var quotation = Quotation.Create(Guid.NewGuid(), "ORC-001", Guid.NewGuid(), DateTime.UtcNow, null, null, null);
        var item = QuotationItem.Create(null, "Test Service", "un", 100m, 2, 0); // Total 200
        quotation.AddItem(item);

        // Act
        quotation.SetDeposit(DepositType.Percentage, 50m, "Metade no aceite");

        // Assert
        Assert.NotNull(quotation.DepositInfo);
        Assert.Equal(50m, quotation.DepositInfo.Value);
        Assert.Equal(100m, quotation.DepositInfo.RequiredAmount);
        Assert.Equal(100m, quotation.DepositInfo.RemainingBalance);
    }

    [Fact]
    public void SetDeposit_FixedAmount_ShouldCalculateRemainingBalance()
    {
        // Arrange
        var quotation = Quotation.Create(Guid.NewGuid(), "ORC-001", Guid.NewGuid(), DateTime.UtcNow, null, null, null);
        var item = QuotationItem.Create(null, "Test Service", "un", 250m, 1, 0); // Total 250
        quotation.AddItem(item);

        // Act
        quotation.SetDeposit(DepositType.FixedAmount, 100m, "Sinal fixo");

        // Assert
        Assert.NotNull(quotation.DepositInfo);
        Assert.Equal(100m, quotation.DepositInfo.Value);
        Assert.Equal(100m, quotation.DepositInfo.RequiredAmount);
        Assert.Equal(150m, quotation.DepositInfo.RemainingBalance);
    }

    [Fact]
    public void MarkAsPending_ShouldChangeStatus()
    {
        // Arrange
        var quotation = Quotation.Create(Guid.NewGuid(), "ORC-001", Guid.NewGuid(), DateTime.UtcNow, null, null, null);
        var item = QuotationItem.Create(null, "Test Service", "un", 100m, 1, 0);
        quotation.AddItem(item);

        // Act
        quotation.MarkAsPending();

        // Assert
        Assert.Equal(QuotationStatus.Pending, quotation.Status);
    }

    [Fact]
    public void MarkAsPending_WithoutItems_ShouldThrowException()
    {
        // Arrange
        var quotation = Quotation.Create(Guid.NewGuid(), "ORC-001", Guid.NewGuid(), DateTime.UtcNow, null, null, null);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => quotation.MarkAsPending());
    }
}
