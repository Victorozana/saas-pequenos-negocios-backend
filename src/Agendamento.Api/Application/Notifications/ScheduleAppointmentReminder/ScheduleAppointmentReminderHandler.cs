using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Notifications;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Application.Notifications.ScheduleAppointmentReminder;

public class ScheduleAppointmentReminderHandler
{
    private readonly AgendamentoDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public ScheduleAppointmentReminderHandler(AgendamentoDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<bool> HandleAsync(ScheduleAppointmentReminderCommand request, CancellationToken cancellationToken = default)
    {
        var appointment = await _dbContext.Appointments
            .Include(a => a.WorkOrder)
                .ThenInclude(w => w!.Customer)
            .FirstOrDefaultAsync(a => a.Id == request.AppointmentId, cancellationToken);

        if (appointment == null)
            return false;

        var phone = appointment.WorkOrder?.Customer?.Phone;
        if (string.IsNullOrWhiteSpace(phone))
            return false; // Cannot send WhatsApp if there is no phone

        var variables = new Dictionary<string, string>
        {
            { "NomeCliente", appointment.WorkOrder?.Customer?.Name ?? "Cliente" },
            { "DataAgendamento", appointment.StartTime.ToString("dd/MM/yyyy") },
            { "HoraAgendamento", appointment.StartTime.ToString(@"hh\:mm") },
            { "NomeServico", appointment.Type.ToString() }
        };

        var template = new NotificationTemplate(
            "AppointmentReminder", 
            "Olá {{NomeCliente}}, lembrando que você tem um horário marcado para {{NomeServico}} no dia {{DataAgendamento}} às {{HoraAgendamento}}.", 
            variables);

        var content = template.Render();

        var notification = NotificationMessage.Create(
            _tenantContext.TenantId,
            phone,
            NotificationChannel.WhatsApp,
            content);

        _dbContext.NotificationMessages.Add(notification);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
