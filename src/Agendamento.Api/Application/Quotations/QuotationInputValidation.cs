namespace Agendamento.Api.Application.Quotations;

internal static class QuotationInputValidation
{
    internal static void Validate(string code, DateTime issueDate, DateTime? validUntil,
        string? notes, string? paymentTerms, string? depositPaymentNotes)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 50 || notes?.Length > 2000 ||
            paymentTerms?.Length > 2000 || depositPaymentNotes?.Length > 1000)
            throw new ArgumentException("Quotation text exceeds its supported limits.");

        // PostgreSQL timestamptz requires UTC. Reject absent/unqualified dates
        // before persistence instead of turning invalid input into a database error.
        if (issueDate.Kind != DateTimeKind.Utc ||
            (validUntil.HasValue && validUntil.Value.Kind != DateTimeKind.Utc))
            throw new ArgumentException("Quotation dates must use UTC.");
    }
}
