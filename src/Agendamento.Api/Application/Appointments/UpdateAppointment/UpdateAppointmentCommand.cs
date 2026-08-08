using System.Text.Json.Serialization;
using Agendamento.Api.Domain.Appointments;

namespace Agendamento.Api.Application.Appointments.UpdateAppointment;

public class UpdateAppointmentCommand
{
    [JsonIgnore]
    public Guid Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public Guid? AssignedToUserId { get; set; }
}
