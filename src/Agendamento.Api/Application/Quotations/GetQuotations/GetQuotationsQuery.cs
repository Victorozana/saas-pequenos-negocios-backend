using Agendamento.Api.Domain.Quotations;

namespace Agendamento.Api.Application.Quotations.GetQuotations;

public record GetQuotationsQuery(
    string? SearchTerm,
    QuotationStatus? Status,
    int Page = 1,
    int PageSize = 20);

public record QuotationListDto(
    Guid Id,
    string Code,
    string CustomerName,
    DateTime IssueDate,
    DateTime? ValidUntil,
    QuotationStatus Status,
    decimal TotalAmount);
    
public record PagedResult<T>(
    IEnumerable<T> Items,
    int TotalCount,
    int Page,
    int PageSize);
