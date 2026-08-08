namespace Agendamento.Api.Application.Appointments.GetAppointments;

public class GetAppointmentsQuery
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public Guid? WorkOrderId { get; set; }
}
