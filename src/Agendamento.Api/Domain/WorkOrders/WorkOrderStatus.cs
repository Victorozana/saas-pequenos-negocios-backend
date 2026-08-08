namespace Agendamento.Api.Domain.WorkOrders;

public enum WorkOrderStatus
{
    Scheduled = 1,
    InProgress = 2,
    WaitingForMaterial = 3,
    Completed = 4,
    Cancelled = 5
}
