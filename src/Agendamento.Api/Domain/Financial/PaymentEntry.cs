namespace Agendamento.Api.Domain.Financial;

public class PaymentEntry
{
    public Guid Id { get; private set; }
    public DateTime PaymentDate { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentMethod Method { get; private set; }
    public string? Notes { get; private set; }

    private PaymentEntry() { } // EF Core

    public static PaymentEntry Create(decimal amount, PaymentMethod method, DateTime paymentDate, string? notes = null)
    {
        if (amount <= 0)
            throw new ArgumentException("Payment amount must be greater than zero.", nameof(amount));

        return new PaymentEntry
        {
            Id = Guid.NewGuid(),
            Amount = amount,
            Method = method,
            PaymentDate = paymentDate.ToUniversalTime(),
            Notes = notes
        };
    }
}
