namespace Agendamento.Api.Domain.Quotations;

internal static class QuotationMoney
{
    public const decimal Maximum = 9999999999999999.99m;

    public static void Validate(decimal value, string parameter)
    {
        if (value < 0 || value > Maximum || decimal.Round(value, 2) != value)
            throw new ArgumentException("Amount must be nonnegative, have at most two decimal places and fit numeric(18,2).", parameter);
    }

    public static decimal Subtotal(decimal unitPrice, decimal quantity)
    {
        decimal amount;
        try { amount = decimal.Round(unitPrice * quantity, 2, MidpointRounding.AwayFromZero); }
        catch (OverflowException) { throw new ArgumentException("Item subtotal exceeds the monetary limit.", nameof(quantity)); }
        Validate(amount, nameof(quantity));
        return amount;
    }
}
