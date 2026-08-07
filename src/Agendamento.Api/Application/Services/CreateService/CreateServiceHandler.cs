using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Services;
using Agendamento.Api.Infrastructure.Persistence;

namespace Agendamento.Api.Application.Services.CreateService;

public class CreateServiceHandler
{
    private readonly AgendamentoDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public CreateServiceHandler(AgendamentoDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> HandleAsync(CreateServiceCommand command, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant)
            throw new InvalidOperationException("Unauthenticated tenant context.");

        var serviceItem = ServiceItem.Create(
            _tenantContext.TenantId,
            command.Name,
            command.Description,
            command.Unit,
            command.BasePrice);

        _dbContext.ServiceItems.Add(serviceItem);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return serviceItem.Id;
    }
}
