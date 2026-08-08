using System.Text.Json.Serialization;
using Agendamento.Api.Domain.WorkOrders;

namespace Agendamento.Api.Application.WorkOrders.UpdateWorkOrderStatus;

public class UpdateWorkOrderStatusCommand
{
    [JsonIgnore]
    public Guid Id { get; set; }
    public WorkOrderStatus Status { get; set; }
    public string? Notes { get; set; }
}
