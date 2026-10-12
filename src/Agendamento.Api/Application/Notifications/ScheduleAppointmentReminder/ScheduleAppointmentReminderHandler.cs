using Agendamento.Api.Domain.Notifications;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Application.Notifications.ScheduleAppointmentReminder;

public class ScheduleAppointmentReminderHandler
{
    private readonly AgendamentoDbContext _dbContext;
    private readonly INotificationService _notifications;

    public ScheduleAppointmentReminderHandler(AgendamentoDbContext dbContext, INotificationService notifications)
    {
        _dbContext = dbContext;
        _notifications = notifications;
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

        var reminder = await _notifications.QueueAutomaticReminderAsync(
            appointment.WorkOrder!.CustomerId,
            phone,
            NotificationChannel.WhatsApp,
            content, cancellationToken);

        if (reminder == null) return false;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
