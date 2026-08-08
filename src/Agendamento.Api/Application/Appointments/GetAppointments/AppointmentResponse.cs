using Agendamento.Api.Domain.Appointments;

namespace Agendamento.Api.Application.Appointments.GetAppointments;

public class AppointmentResponse
{
    public Guid Id { get; set; }
    public Guid? WorkOrderId { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public Guid? AssignedToUserId { get; set; }

    public static AppointmentResponse FromEntity(Appointment appointment)
    {
        return new AppointmentResponse
        {
            Id = appointment.Id,
            WorkOrderId = appointment.WorkOrderId,
            Type = appointment.Type.ToString(),
            StartTime = appointment.StartTime,
            EndTime = appointment.EndTime,
            Address = appointment.Address,
            Notes = appointment.Notes,
            AssignedToUserId = appointment.AssignedToUserId
        };
    }
}
