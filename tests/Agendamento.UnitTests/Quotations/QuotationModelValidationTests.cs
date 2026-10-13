using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Domain.Services;
using Agendamento.Api.Domain.WorkOrders;
using Agendamento.Api.Application.WorkOrders.GetWorkOrderById;
using Xunit;

namespace Agendamento.UnitTests.Quotations;

public class QuotationModelValidationTests
{
    private const decimal Maximum = 9999999999999999.99m;
    private static Quotation NewQuotation() => Quotation.Create(Guid.NewGuid(), "MODEL-001", Guid.NewGuid(), DateTime.UtcNow.Date, null, "Notes", "Terms");
    private static QuotationItem Item(decimal price, decimal quantity = 1m, decimal discount = 0m) => QuotationItem.Create(null, "Service", "m²", price, quantity, discount);

    [Fact(DisplayName = "Tenant obrigatório e atualização inválida do catálogo preserva conteúdo @spec:AC-045")]
    public void TenantAndServiceValidationAreAtomic()
    {
        Assert.Throws<ArgumentException>(() => ServiceItem.Create(Guid.Empty, "Service", null, "un", 1m));
        Assert.Throws<ArgumentException>(() => Quotation.Create(Guid.Empty, "Q", Guid.NewGuid(), DateTime.UtcNow, null, null, null));
        var service = ServiceItem.Create(Guid.NewGuid(), "Original", "Description", "un", 20m);
        foreach (var invalid in new[] { -1m, 1.001m, Maximum + 0.01m, decimal.MaxValue })
        {
            Assert.Throws<ArgumentException>(() => service.Update("Changed", "Changed", "hour", invalid));
            Assert.Equal("Original", service.Name);
            Assert.Equal("Description", service.Description);
            Assert.Equal("un", service.Unit);
            Assert.Equal(20m, service.BasePrice);
        }
        service.Update("Valid", null, "hour", Maximum);
        Assert.Equal(Maximum, service.BasePrice);
        Assert.Equal("Valid", service.Name);
    }

    [Fact(DisplayName = "Valores monetários aceitam zero e teto e rejeitam negativos excesso e frações @spec:AC-046")]
    public void MonetaryInputBoundaries()
    {
        Assert.Equal(0m, ServiceItem.Create(Guid.NewGuid(), "Free", null, "un", 0m).BasePrice);
        Assert.Equal(Maximum, ServiceItem.Create(Guid.NewGuid(), "Maximum", null, "un", Maximum).BasePrice);
        Assert.Equal(Maximum, Item(Maximum).TotalPrice);
        Assert.Equal(0m, Item(10m, 1m, 10m).TotalPrice);
        Assert.Equal(0m, Item(0m, 0.01m).TotalPrice);
        Assert.Equal(0m, Item(0m, Maximum).TotalPrice);
        foreach (var invalid in new[] { -0.01m, 0.001m, Maximum + 0.01m, decimal.MaxValue })
        {
            Assert.Throws<ArgumentException>(() => ServiceItem.Create(Guid.NewGuid(), "Service", null, "un", invalid));
            Assert.Throws<ArgumentException>(() => Item(invalid));
            Assert.Throws<ArgumentException>(() => Item(Maximum, 1m, invalid));
        }
        Assert.Throws<ArgumentException>(() => Item(10m, 0m));
        Assert.Throws<ArgumentException>(() => Item(10m, -1m));
        Assert.Throws<ArgumentException>(() => Item(0m, 0.001m));
        Assert.Throws<ArgumentException>(() => Item(0m, Maximum + 0.01m));
        Assert.Throws<ArgumentException>(() => Item(10m, 1m, 10.01m));
        Assert.Throws<ArgumentException>(() => Item(Maximum, 2m));
        Assert.Throws<ArgumentException>(() => Item(Maximum, decimal.MaxValue));
    }

    [Theory(DisplayName = "Consumidor de OS mantém arredondamento do subtotal do orçamento @spec:AC-046")]
    [InlineData(0.0)]
    [InlineData(0.01)]
    public void WorkOrderResponseUsesRoundedQuotationSubtotal(double discountInput)
    {
        var discount = (decimal)discountInput;
        var quotationItem = Item(0.05m, 0.5m, discount);
        var workOrderItem = WorkOrderItem.Create(null, "Service", "m²", 0.5m, 0.05m, discount);
        var response = WorkOrderItemResponse.FromEntity(workOrderItem);
        Assert.Equal(0.03m - discount, response.TotalAmount);
        Assert.Equal(quotationItem.TotalPrice, response.TotalAmount);
        Assert.Equal(0.5m, response.Quantity);
        Assert.Equal(0.05m, response.UnitPrice);
        Assert.Equal(discount, response.DiscountAmount);
    }

    [Fact(DisplayName = "Subtotal arredonda por item e entrada percentual conserva total em centavos @spec:AC-046")]
    public void ItemAndPercentageRoundingConserveMoney()
    {
        var quotation = NewQuotation();
        quotation.AddItem(Item(0.01m, 0.5m));
        quotation.AddItem(Item(0.01m, 0.5m));
        Assert.All(quotation.Items, item => Assert.Equal(0.01m, item.TotalPrice));
        Assert.Equal(0.02m, quotation.SubtotalAmount);
        Assert.Equal(0.02m, quotation.TotalAmount);
        quotation.SetDeposit(DepositType.Percentage, 25m, null);
        Assert.Equal(0.01m, quotation.DepositInfo!.RequiredAmount);
        Assert.Equal(0.01m, quotation.DepositInfo.RemainingBalance);
        Assert.Equal(quotation.TotalAmount, quotation.DepositInfo.RequiredAmount + quotation.DepositInfo.RemainingBalance);
        var discounted = Item(0.03m, 0.5m, 0.01m);
        Assert.Equal(0.01m, discounted.TotalPrice);
        var maximumDeposit = DepositInfo.Create(DepositType.Percentage, 100m, Maximum, null);
        Assert.Equal(Maximum, maximumDeposit.RequiredAmount);
        Assert.Equal(0m, maximumDeposit.RemainingBalance);
    }

    [Fact(DisplayName = "Entrada rejeita tipo inválido e limites mantendo sinal anterior @spec:AC-046")]
    public void InvalidDepositDoesNotReplaceExistingDeposit()
    {
        var quotation = NewQuotation();
        quotation.AddItem(Item(100m));
        quotation.SetDeposit(DepositType.FixedAmount, 20m, "Original");
        var original = quotation.DepositInfo;
        foreach (var invalid in new[] { -0.01m, 20.001m, 100.01m, Maximum + 0.01m })
        {
            Assert.Throws<ArgumentException>(() => quotation.SetDeposit(DepositType.FixedAmount, invalid, "Changed"));
            Assert.Same(original, quotation.DepositInfo);
        }
        foreach (var invalid in new[] { -0.01m, 0.001m, 100.01m, decimal.MaxValue })
        {
            Assert.Throws<ArgumentException>(() => quotation.SetDeposit(DepositType.Percentage, invalid, null));
            Assert.Same(original, quotation.DepositInfo);
        }
        Assert.Throws<ArgumentException>(() => quotation.SetDeposit((DepositType)999, 1m, null));
        Assert.Same(original, quotation.DepositInfo);
        Assert.Throws<ArgumentException>(() => DepositInfo.Create(DepositType.Percentage, 50m, -1m, null));
        Assert.Throws<ArgumentException>(() => DepositInfo.Create(DepositType.Percentage, 50m, Maximum + 0.01m, null));
        foreach (var type in new[] { DepositType.FixedAmount, DepositType.Percentage })
        {
            var zero = DepositInfo.Create(type, 0m, 0m, null);
            Assert.Equal(0m, zero.RequiredAmount);
            Assert.Equal(0m, zero.RemainingBalance);
        }
    }

    [Fact(DisplayName = "Adicionar e remover itens recusados preserva itens totais e entrada @spec:AC-046")]
    public void RefusedItemChangesAreAtomic()
    {
        var quotation = NewQuotation();
        var first = Item(60m);
        quotation.AddItem(first);
        quotation.AddItem(Item(40m));
        quotation.SetDeposit(DepositType.FixedAmount, 80m, "Deposit");
        var originalItems = quotation.Items.ToArray();
        var originalDeposit = quotation.DepositInfo;
        Assert.Throws<ArgumentException>(() => quotation.RemoveItem(first.Id));
        Assert.Equal(originalItems, quotation.Items);
        Assert.Equal(100m, quotation.SubtotalAmount);
        Assert.Equal(0m, quotation.DiscountAmount);
        Assert.Equal(100m, quotation.TotalAmount);
        Assert.Same(originalDeposit, quotation.DepositInfo);
        Assert.ThrowsAny<ArgumentException>(() => quotation.AddItem(null!));
        Assert.Equal(originalItems, quotation.Items);
        var extreme = NewQuotation();
        extreme.AddItem(Item(Maximum));
        var original = Assert.Single(extreme.Items);
        Assert.Throws<ArgumentException>(() => extreme.AddItem(Item(0.01m)));
        Assert.Same(original, Assert.Single(extreme.Items));
        Assert.Equal(Maximum, extreme.TotalAmount);
        quotation.RemoveItem(Guid.NewGuid());
        Assert.Equal(originalItems, quotation.Items);
    }

    [Fact(DisplayName = "Recalcular itens válidos atualiza sinal percentual e fixo @spec:AC-046")]
    public void ValidItemChangesRecalculateDeposit()
    {
        foreach (var type in new[] { DepositType.Percentage, DepositType.FixedAmount })
        {
            var quotation = NewQuotation();
            quotation.AddItem(Item(100m, 2m, 10m));
            quotation.SetDeposit(type, 50m, "Notes");
            var extra = Item(60m, 0.5m, 10m);
            quotation.AddItem(extra);
            Assert.Equal(230m, quotation.SubtotalAmount);
            Assert.Equal(20m, quotation.DiscountAmount);
            Assert.Equal(210m, quotation.TotalAmount);
            Assert.Equal(type == DepositType.Percentage ? 105m : 50m, quotation.DepositInfo!.RequiredAmount);
            Assert.Equal(quotation.TotalAmount, quotation.DepositInfo.RequiredAmount + quotation.DepositInfo.RemainingBalance);
            quotation.RemoveItem(extra.Id);
            Assert.Equal(190m, quotation.TotalAmount);
            Assert.Equal(type == DepositType.Percentage ? 95m : 50m, quotation.DepositInfo!.RequiredAmount);
        }
    }

    [Fact(DisplayName = "Edição pendente inválida preserva identidade conteúdo e valores @spec:AC-046")]
    public void InvalidPendingReplacementIsAtomic()
    {
        var quotation = NewQuotation();
        var item = Item(100m);
        quotation.AddItem(item);
        quotation.SetDeposit(DepositType.FixedAmount, 20m, "Original");
        quotation.MarkAsPending();
        var id = quotation.Id;
        var tenant = quotation.TenantId;
        var customer = quotation.CustomerId;
        var issue = quotation.IssueDate;
        var deposit = quotation.DepositInfo;
        Assert.Throws<ArgumentException>(() => quotation.UpdatePendingContent("Changed", Guid.NewGuid(), issue, null, "Changed", "Changed", new[] { Item(10m) }, DepositType.FixedAmount, 20m, "Changed"));
        Assert.Equal(id, quotation.Id);
        Assert.Equal(tenant, quotation.TenantId);
        Assert.Equal(customer, quotation.CustomerId);
        Assert.Equal(issue, quotation.IssueDate);
        Assert.Equal("MODEL-001", quotation.Code);
        Assert.Equal("Notes", quotation.Notes);
        Assert.Equal("Terms", quotation.PaymentTerms);
        Assert.Equal(QuotationStatus.Pending, quotation.Status);
        Assert.Same(item, Assert.Single(quotation.Items));
        Assert.Equal(100m, quotation.SubtotalAmount);
        Assert.Equal(0m, quotation.DiscountAmount);
        Assert.Equal(100m, quotation.TotalAmount);
        Assert.Same(deposit, quotation.DepositInfo);
    }

    [Theory(DisplayName = "Conteúdo decidido ou encerrado permanece imutável @spec:AC-046")]
    [InlineData(QuotationStatus.Approved)]
    [InlineData(QuotationStatus.Rejected)]
    [InlineData(QuotationStatus.Converted)]
    [InlineData(QuotationStatus.Canceled)]
    [InlineData(QuotationStatus.Expired)]
    public void FinalStatesRejectContentChanges(QuotationStatus status)
    {
        var quotation = Quotation.Create(Guid.NewGuid(), "FINAL", Guid.NewGuid(), DateTime.UtcNow.Date.AddDays(-2), status == QuotationStatus.Expired ? DateTime.UtcNow.Date.AddDays(-1) : null, null, null);
        var item = Item(100m);
        quotation.AddItem(item);
        quotation.SetDeposit(DepositType.FixedAmount, 20m, "Original");
        quotation.MarkAsPending();
        switch (status)
        {
            case QuotationStatus.Approved: quotation.Approve(); break;
            case QuotationStatus.Rejected: quotation.Reject(); break;
            case QuotationStatus.Converted: quotation.Approve(); quotation.MarkAsConverted(); break;
            case QuotationStatus.Canceled: quotation.Cancel(); break;
            case QuotationStatus.Expired: quotation.CheckExpiration(); break;
        }
        var deposit = quotation.DepositInfo;
        Assert.Throws<InvalidOperationException>(() => quotation.AddItem(Item(1m)));
        Assert.Throws<InvalidOperationException>(() => quotation.RemoveItem(item.Id));
        Assert.Throws<InvalidOperationException>(() => quotation.SetDeposit(DepositType.FixedAmount, 1m, null));
        Assert.Throws<InvalidOperationException>(() => quotation.RemoveDeposit());
        Assert.Throws<InvalidOperationException>(() => quotation.UpdatePendingContent("Changed", Guid.NewGuid(), DateTime.UtcNow, null, null, null, new[] { Item(1m) }, null, 0m, null));
        Assert.Equal(status, quotation.Status);
        Assert.Equal("FINAL", quotation.Code);
        Assert.Same(item, Assert.Single(quotation.Items));
        Assert.Equal(100m, quotation.TotalAmount);
        Assert.Same(deposit, quotation.DepositInfo);
    }
}
