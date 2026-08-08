using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Application.WorkOrders.GetWorkOrderById;

public class GetWorkOrderByIdHandler
{
    private readonly AgendamentoDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetWorkOrderByIdHandler(
        AgendamentoDbContext dbContext,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<WorkOrderResponse?> HandleAsync(GetWorkOrderByIdQuery query, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant)
            throw new InvalidOperationException("Unauthenticated tenant context.");

        var workOrder = await _dbContext.WorkOrders
            .Include(wo => wo.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(wo => wo.Id == query.Id, cancellationToken);

        if (workOrder == null)
            return null;

        return WorkOrderResponse.FromEntity(workOrder);
    }
}
