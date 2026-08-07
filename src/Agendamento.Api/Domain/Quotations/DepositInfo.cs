namespace Agendamento.Api.Domain.Quotations;

public record DepositInfo
{
    public DepositType Type { get; init; }
    public decimal Value { get; init; }
    public decimal RequiredAmount { get; init; }
    public decimal RemainingBalance { get; init; }
    public string? PaymentNotes { get; init; }
    public bool IsPaid { get; init; }

    private DepositInfo() { } // EF Core

    private DepositInfo(DepositType type, decimal value, decimal requiredAmount, decimal remainingBalance, string? paymentNotes, bool isPaid)
    {
        Type = type;
        Value = value;
        RequiredAmount = requiredAmount;
        RemainingBalance = remainingBalance;
        PaymentNotes = paymentNotes;
        IsPaid = isPaid;
    }

    public static DepositInfo Create(DepositType type, decimal value, decimal totalAmount, string? paymentNotes)
    {
        if (value < 0)
            throw new ArgumentException("Deposit value cannot be negative.", nameof(value));

        if (totalAmount < 0)
            throw new ArgumentException("Total amount cannot be negative.", nameof(totalAmount));

        decimal requiredAmount = 0m;

        if (type == DepositType.Percentage)
        {
            if (value > 100)
                throw new ArgumentException("Percentage cannot exceed 100.", nameof(value));
            
            requiredAmount = (value / 100m) * totalAmount;
        }
        else if (type == DepositType.FixedAmount)
        {
            if (value > totalAmount)
                throw new ArgumentException("Fixed deposit amount cannot exceed total amount.", nameof(value));
                
            requiredAmount = value;
        }

        var remainingBalance = totalAmount - requiredAmount;

        return new DepositInfo(type, value, requiredAmount, remainingBalance, paymentNotes, false);
    }
    
    public DepositInfo MarkAsPaid()
    {
        return this with { IsPaid = true };
    }
}
