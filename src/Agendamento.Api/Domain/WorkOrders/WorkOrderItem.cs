using Agendamento.Api.Domain.Quotations;

namespace Agendamento.Api.Domain.WorkOrders;

public class WorkOrderItem
{
    public Guid Id { get; private set; }
    public Guid WorkOrderId { get; private set; }
    public Guid? ServiceItemId { get; private set; }
    public string ServiceName { get; private set; } = string.Empty;
    public string Unit { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal DiscountAmount { get; private set; }

    private WorkOrderItem() { }

    public static WorkOrderItem Create(Guid? serviceItemId, string serviceName, string unit, decimal quantity, decimal unitPrice, decimal discountAmount)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            throw new ArgumentException("Service name cannot be empty.", nameof(serviceName));
            
        if (string.IsNullOrWhiteSpace(unit))
            throw new ArgumentException("Unit cannot be empty.", nameof(unit));

        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));

        QuotationMoney.Validate(quantity, nameof(quantity));

        QuotationMoney.Validate(unitPrice, nameof(unitPrice));
        QuotationMoney.Validate(discountAmount, nameof(discountAmount));
        if (discountAmount > QuotationMoney.Subtotal(unitPrice, quantity))
            throw new ArgumentException("Discount cannot exceed subtotal.", nameof(discountAmount));

        return new WorkOrderItem
        {
            Id = Guid.NewGuid(),
            ServiceItemId = serviceItemId,
            ServiceName = serviceName,
            Unit = unit,
            Quantity = quantity,
            UnitPrice = unitPrice,
            DiscountAmount = discountAmount
        };
    }
}
