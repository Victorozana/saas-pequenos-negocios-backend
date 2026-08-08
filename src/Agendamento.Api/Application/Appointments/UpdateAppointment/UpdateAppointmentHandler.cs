using Agendamento.Api.Application.Common;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Application.Appointments.UpdateAppointment;

public class UpdateAppointmentHandler
{
    private readonly AgendamentoDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateAppointmentHandler(
        AgendamentoDbContext dbContext,
        ITenantContext tenantContext,
        IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(UpdateAppointmentCommand command, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant)
            throw new InvalidOperationException("Unauthenticated tenant context.");

        if (command.EndTime <= command.StartTime)
            throw new ArgumentException("End time must be after start time.");

        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(a => a.Id == command.Id, cancellationToken);

        if (appointment == null)
            throw new InvalidOperationException("Appointment not found.");

        if (command.AssignedToUserId.HasValue)
        {
            bool hasConflict = await _dbContext.Appointments.AnyAsync(a =>
                a.Id != command.Id &&
                a.AssignedToUserId == command.AssignedToUserId.Value &&
                a.StartTime < command.EndTime &&
                a.EndTime > command.StartTime, cancellationToken);

            if (hasConflict)
                throw new InvalidOperationException("Scheduling conflict: The assigned user already has an appointment during this time period.");
        }

        appointment.Reschedule(command.StartTime, command.EndTime);
        
        if (command.AssignedToUserId.HasValue)
        {
            appointment.AssignTo(command.AssignedToUserId.Value);
        }

        await _unitOfWork.CommitAsync(cancellationToken);
    }
}
