using Agendamento.Api.Application.Common;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Application.WorkOrders.UpdateWorkOrderStatus;

public class UpdateWorkOrderStatusHandler
{
    private readonly AgendamentoDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateWorkOrderStatusHandler(
        AgendamentoDbContext dbContext,
        ITenantContext tenantContext,
        IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(UpdateWorkOrderStatusCommand command, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant)
            throw new InvalidOperationException("Unauthenticated tenant context.");

        var workOrder = await _dbContext.WorkOrders
            .FirstOrDefaultAsync(wo => wo.Id == command.Id, cancellationToken);

        if (workOrder == null)
            throw new InvalidOperationException("Work Order not found.");

        workOrder.UpdateStatus(command.Status, command.Notes);

        await _unitOfWork.CommitAsync(cancellationToken);
    }
}
