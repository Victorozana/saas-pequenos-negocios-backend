using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Domain.Services;
using Xunit;

namespace Agendamento.UnitTests.Quotations;

public class QuotationDomainTests
{
    [Fact(DisplayName = "ServiceItem e Quotation implementam ITenantOwned com TenantId @spec:AC-045")]
    public void ServiceItem_And_Quotation_ShouldImplement_ITenantOwned()
    {
        var tenantId = Guid.NewGuid();
        var service = ServiceItem.Create(tenantId, "Corte de Chapa", "Corte sob medida", "m2", 150m);
        var quotation = Quotation.Create(tenantId, "ORC-001", Guid.NewGuid(), DateTime.UtcNow, null, null, null);

        Assert.Equal(tenantId, service.TenantId);
        Assert.Equal(tenantId, quotation.TenantId);
        Assert.IsAssignableFrom<Agendamento.Domain.Common.ITenantOwned>(service);
        Assert.IsAssignableFrom<Agendamento.Domain.Common.ITenantOwned>(quotation);
    }

    [Fact(DisplayName = "Calcula subtotais, descontos e total geral do orçamento @spec:AC-046")]
    public void AddItem_ShouldRecalculateTotals()
    {
        // Arrange
        var quotation = Quotation.Create(Guid.NewGuid(), "ORC-001", Guid.NewGuid(), DateTime.UtcNow, null, null, null);
        var item1 = QuotationItem.Create(null, "Test Service 1", "un", 100m, 2, 50m); // 200 - 50 = 150
        var item2 = QuotationItem.Create(null, "Test Service 2", "m2", 50m, 3, 0m);  // 150 - 0 = 150

        // Act
        quotation.AddItem(item1);
        quotation.AddItem(item2);

        // Assert
        Assert.Equal(350m, quotation.SubtotalAmount);
        Assert.Equal(50m, quotation.DiscountAmount);
        Assert.Equal(300m, quotation.TotalAmount);
    }

    [Fact(DisplayName = "Calcula entrada exigida em porcentagem e saldo restante @spec:AC-046")]
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

    [Fact(DisplayName = "Calcula entrada exigida em valor fixo e saldo restante @spec:AC-046")]
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

    [Fact(DisplayName = "Transições de status do orçamento seguem validações de negócio @spec:AC-048")]
    public void StatusTransitions_ShouldFollowBusinessRules()
    {
        // Arrange
        var quotation = Quotation.Create(Guid.NewGuid(), "ORC-001", Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(7), null, null);
        var item = QuotationItem.Create(null, "Test Service", "un", 100m, 1, 0);
        quotation.AddItem(item);

        // Act & Assert Draft -> Pending
        quotation.MarkAsPending();
        Assert.Equal(QuotationStatus.Pending, quotation.Status);

        // Pending -> Approved
        quotation.Approve();
        Assert.Equal(QuotationStatus.Approved, quotation.Status);

        // Approved -> Converted
        quotation.MarkAsConverted();
        Assert.Equal(QuotationStatus.Converted, quotation.Status);
    }

    [Fact(DisplayName = "Marcação de pendente em orçamento sem itens lança exceção @spec:AC-048")]
    public void MarkAsPending_WithoutItems_ShouldThrowException()
    {
        // Arrange
        var quotation = Quotation.Create(Guid.NewGuid(), "ORC-001", Guid.NewGuid(), DateTime.UtcNow, null, null, null);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => quotation.MarkAsPending());
    }

    [Fact(DisplayName = "Aprovação de orçamento expirado lança exceção @spec:AC-048")]
    public void Approve_ExpiredQuotation_ShouldThrowException()
    {
        // Arrange (expirado ontem)
        var quotation = Quotation.Create(Guid.NewGuid(), "ORC-001", Guid.NewGuid(), DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(-1), null, null);
        var item = QuotationItem.Create(null, "Test Service", "un", 100m, 1, 0);
        quotation.AddItem(item);
        quotation.MarkAsPending();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => quotation.Approve());
    }
}
