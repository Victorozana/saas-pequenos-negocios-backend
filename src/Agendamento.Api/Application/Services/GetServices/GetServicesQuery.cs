namespace Agendamento.Api.Application.Services.GetServices;

public record GetServicesQuery(
    string? SearchTerm,
    bool? IsActive,
    int Page = 1,
    int PageSize = 20);
    
public record ServiceItemDto(
    Guid Id,
    string Name,
    string? Description,
    string Unit,
    decimal BasePrice,
    bool IsActive);
    
public record PagedResult<T>(
    IEnumerable<T> Items,
    int TotalCount,
    int Page,
    int PageSize);
