using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Application.Customers.GetCustomers;

public class GetCustomerByIdHandler
{
    private readonly AgendamentoDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetCustomerByIdHandler(AgendamentoDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<CustomerResponse?> HandleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new InvalidOperationException("Unauthenticated tenant context.");
        }

        var c = await _dbContext.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (c == null)
            return null;

        return new CustomerResponse(
            c.Id,
            c.Name,
            c.Phone,
            c.Email,
            c.Document?.Type,
            c.Document?.Value,
            c.Address?.Street,
            c.Address?.Number,
            c.Address?.Complement,
            c.Address?.Neighborhood,
            c.Address?.City,
            c.Address?.State,
            c.Address?.ZipCode,
            c.IsActive
        );
    }
}
