using System.Text.Json.Serialization;
using Agendamento.Api.Domain.Appointments;

namespace Agendamento.Api.Application.Appointments.CreateAppointment;

public class CreateAppointmentCommand
{
    public Guid? WorkOrderId { get; set; }
    public AppointmentType Type { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public Guid? AssignedToUserId { get; set; }
}
