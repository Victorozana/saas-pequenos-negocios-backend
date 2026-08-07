namespace Agendamento.Api.Domain.Quotations;

public class QuotationItem
{
    public Guid Id { get; private set; }
    public Guid QuotationId { get; private set; }
    public Guid? ServiceItemId { get; private set; }
    
    public string ServiceName { get; private set; } = string.Empty;
    public string Unit { get; private set; } = string.Empty;
    
    public decimal UnitPrice { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal DiscountAmount { get; private set; }
    
    public decimal TotalPrice { get; private set; }

    private QuotationItem() { } // EF Core

    public static QuotationItem Create(Guid? serviceItemId, string serviceName, string unit, decimal unitPrice, decimal quantity, decimal discountAmount)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            throw new ArgumentException("Service name cannot be empty.", nameof(serviceName));
            
        if (string.IsNullOrWhiteSpace(unit))
            throw new ArgumentException("Unit cannot be empty.", nameof(unit));

        if (unitPrice < 0)
            throw new ArgumentException("Unit price cannot be negative.", nameof(unitPrice));
            
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));

        if (discountAmount < 0)
            throw new ArgumentException("Discount cannot be negative.", nameof(discountAmount));

        var subtotal = unitPrice * quantity;
        if (discountAmount > subtotal)
            throw new ArgumentException("Discount cannot exceed subtotal.", nameof(discountAmount));

        return new QuotationItem
        {
            Id = Guid.NewGuid(),
            ServiceItemId = serviceItemId,
            ServiceName = serviceName,
            Unit = unit,
            UnitPrice = unitPrice,
            Quantity = quantity,
            DiscountAmount = discountAmount,
            TotalPrice = subtotal - discountAmount
        };
    }
}
