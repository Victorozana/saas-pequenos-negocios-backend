using Microsoft.EntityFrameworkCore;
using Agendamento.Api.Infrastructure.Persistence;

namespace Agendamento.Api.Application.Dashboard.GetUpcomingSchedule;

public class GetUpcomingScheduleHandler
{
    private readonly AgendamentoDbContext _dbContext;

    public GetUpcomingScheduleHandler(AgendamentoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<UpcomingScheduleDto>> HandleAsync(GetUpcomingScheduleQuery request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var limitDate = now.AddDays(request.DaysAhead);

        var appointments = await _dbContext.Appointments
            .Include(a => a.WorkOrder)
            .ThenInclude(w => w!.Customer)
            .AsNoTracking()
            .Where(a => a.StartTime >= now && a.StartTime <= limitDate)
            .OrderBy(a => a.StartTime)
            .Select(a => new UpcomingScheduleDto
            {
                AppointmentId = a.Id,
                Type = a.Type,
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                Address = a.Address,
                Notes = a.Notes,
                WorkOrderCode = a.WorkOrder != null ? a.WorkOrder.Code : null,
                CustomerName = a.WorkOrder != null && a.WorkOrder.Customer != null ? a.WorkOrder.Customer.Name : null
            })
            .ToListAsync(cancellationToken);

        return appointments;
    }
}
