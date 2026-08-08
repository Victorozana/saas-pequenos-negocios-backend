using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Application.Appointments.GetAppointments;

public class GetAppointmentsHandler
{
    private readonly AgendamentoDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetAppointmentsHandler(
        AgendamentoDbContext dbContext,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<List<AppointmentResponse>> HandleAsync(GetAppointmentsQuery query, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant)
            throw new InvalidOperationException("Unauthenticated tenant context.");

        var queryable = _dbContext.Appointments.AsNoTracking();

        if (query.StartDate.HasValue)
            queryable = queryable.Where(a => a.StartTime >= query.StartDate.Value);

        if (query.EndDate.HasValue)
            queryable = queryable.Where(a => a.EndTime <= query.EndDate.Value);

        if (query.AssignedToUserId.HasValue)
            queryable = queryable.Where(a => a.AssignedToUserId == query.AssignedToUserId.Value);

        if (query.WorkOrderId.HasValue)
            queryable = queryable.Where(a => a.WorkOrderId == query.WorkOrderId.Value);

        var appointments = await queryable
            .OrderBy(a => a.StartTime)
            .ToListAsync(cancellationToken);

        return appointments.Select(AppointmentResponse.FromEntity).ToList();
    }
}
