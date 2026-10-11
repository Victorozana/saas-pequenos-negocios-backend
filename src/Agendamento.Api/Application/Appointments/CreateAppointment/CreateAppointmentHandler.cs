using Agendamento.Api.Application.Common;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Appointments;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Application.Appointments.CreateAppointment;

public class CreateAppointmentHandler
{
    private readonly AgendamentoDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAppointmentHandler(
        AgendamentoDbContext dbContext,
        ITenantContext tenantContext,
        IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> HandleAsync(CreateAppointmentCommand command, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant)
            throw new InvalidOperationException("Unauthenticated tenant context.");

        if (command.EndTime <= command.StartTime)
            throw new ArgumentException("End time must be after start time.");

        Guid? customerId = null;
        if (command.WorkOrderId.HasValue)
        {
            customerId = await _dbContext.WorkOrders
                .Where(w => w.Id == command.WorkOrderId.Value && w.TenantId == _tenantContext.TenantId)
                .Select(w => (Guid?)w.CustomerId)
                .FirstOrDefaultAsync(cancellationToken);

            if (!customerId.HasValue)
                throw new ArgumentException("A ordem de serviço informada não foi encontrada.");
        }

        if (command.AssignedToUserId.HasValue)
        {
            bool hasConflict = await _dbContext.Appointments.AnyAsync(a =>
                a.AssignedToUserId == command.AssignedToUserId.Value &&
                a.StartTime < command.EndTime &&
                a.EndTime > command.StartTime, cancellationToken);

            if (hasConflict)
                throw new InvalidOperationException("Scheduling conflict: The assigned user already has an appointment during this time period.");
        }

        var appointment = Appointment.Create(
            _tenantContext.TenantId,
            command.WorkOrderId,
            command.Type,
            command.StartTime,
            command.EndTime,
            command.Address,
            command.Notes,
            command.AssignedToUserId
        );

        _dbContext.Appointments.Add(appointment);

        if (customerId.HasValue)
        {
            var customerPhone = await _dbContext.Customers
                .Where(c => c.Id == customerId.Value)
                .Select(c => c.Phone)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(customerPhone))
            {
                var notification = Agendamento.Api.Domain.Notifications.NotificationMessage.Create(
                    _tenantContext.TenantId,
                    customerPhone,
                    Agendamento.Api.Domain.Notifications.NotificationChannel.WhatsApp,
                    $"Seu agendamento foi confirmado para {appointment.StartTime.ToLocalTime():dd/MM/yyyy HH:mm}. O endereço é {appointment.Address}."
                );
                _dbContext.NotificationMessages.Add(notification);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return appointment.Id;
    }
}
