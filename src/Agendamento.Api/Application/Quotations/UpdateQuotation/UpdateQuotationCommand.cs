using Agendamento.Api.Application.Quotations.CreateQuotation;

namespace Agendamento.Api.Application.Quotations.UpdateQuotation;

public record UpdateQuotationCommand(
    string Code,
    Guid CustomerId,
    DateTime IssueDate,
    DateTime? ValidUntil,
    string? Notes,
    string? PaymentTerms,
    List<CreateQuotationItemDto> Items,
    CreateDepositDto? Deposit);
