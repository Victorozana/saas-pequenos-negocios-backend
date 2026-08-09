using Agendamento.Api.Domain.Appointments;

namespace Agendamento.Api.Application.Dashboard.GetUpcomingSchedule;

public class UpcomingScheduleDto
{
    public Guid AppointmentId { get; set; }
    public AppointmentType Type { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public string? WorkOrderCode { get; set; }
    public string? CustomerName { get; set; }
}
