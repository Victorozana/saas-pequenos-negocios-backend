using Agendamento.Domain.Common;

namespace Agendamento.Api.Domain.WorkOrders;

public sealed class WorkOrderParticipant : ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid WorkOrderId { get; init; }
    public Guid UserId { get; init; }
}
