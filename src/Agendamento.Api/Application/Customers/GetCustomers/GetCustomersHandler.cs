using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Application.Customers.GetCustomers;

public record CustomerResponse(
    Guid Id,
    string Name,
    string Phone,
    string? Email,
    string? DocumentType,
    string? DocumentValue,
    string? Street,
    string? Number,
    string? Complement,
    string? Neighborhood,
    string? City,
    string? State,
    string? ZipCode,
    bool IsActive,
    bool AutomaticRemindersEnabled = true)
{
    public const string DocumentDisplayPattern = @"^(?:\*{3}\.\*{3}\.\*{3}-[0-9]{2}|[0-9]{14})$";

    public static string? DisplayDocument(Agendamento.Api.Domain.Customers.CustomerDocument? document)
    {
        if (document?.Type == "CPF" && document.Value.Length == 11)
            return $"***.***.***-{document.Value[^2..]}";
        if (document?.Type == "CNPJ" && document.Value.Length == 14)
            return document.Value;
        return null;
    }
}

public record PaginatedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);

public class GetCustomersHandler
{
    private readonly AgendamentoDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetCustomersHandler(AgendamentoDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<PaginatedResult<CustomerResponse>> HandleAsync(string? search = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new InvalidOperationException("Unauthenticated tenant context.");
        }

        var query = _dbContext.Customers.AsNoTracking().Where(c => c.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.ToLower();
            query = query.Where(c => 
                c.Name.ToLower().Contains(search) || 
                c.Phone.Contains(search) || 
                (c.Document != null && c.Document.Value.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var customers = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = customers.Select(c => new CustomerResponse(
            c.Id,
            c.Name,
            c.Phone,
            c.Email,
            c.Document?.Type,
            CustomerResponse.DisplayDocument(c.Document),
            c.Address?.Street,
            c.Address?.Number,
            c.Address?.Complement,
            c.Address?.Neighborhood,
            c.Address?.City,
            c.Address?.State,
            c.Address?.ZipCode,
            c.IsActive,
            c.AutomaticRemindersEnabled
        )).ToList();

        return new PaginatedResult<CustomerResponse>(items, totalCount, page, pageSize);
    }
}
