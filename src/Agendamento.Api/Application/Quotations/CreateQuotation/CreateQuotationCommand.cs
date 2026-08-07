using Agendamento.Api.Domain.Quotations;

namespace Agendamento.Api.Application.Quotations.CreateQuotation;

public record CreateQuotationItemDto(
    Guid? ServiceItemId,
    string ServiceName,
    string Unit,
    decimal UnitPrice,
    decimal Quantity,
    decimal DiscountAmount);

public record CreateDepositDto(
    DepositType Type,
    decimal Value,
    string? PaymentNotes);

public record CreateQuotationCommand(
    string Code,
    Guid CustomerId,
    DateTime IssueDate,
    DateTime? ValidUntil,
    string? Notes,
    string? PaymentTerms,
    List<CreateQuotationItemDto> Items,
    CreateDepositDto? Deposit);
