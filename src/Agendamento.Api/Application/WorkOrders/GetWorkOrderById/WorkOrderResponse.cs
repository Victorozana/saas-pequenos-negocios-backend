using Agendamento.Api.Domain.WorkOrders;

namespace Agendamento.Api.Application.WorkOrders.GetWorkOrderById;

public class WorkOrderItemResponse
{
    public Guid Id { get; set; }
    public Guid ServiceItemId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public static WorkOrderItemResponse FromEntity(WorkOrderItem item)
    {
        return new WorkOrderItemResponse
        {
            Id = item.Id,
            ServiceItemId = item.ServiceItemId,
            ServiceName = item.ServiceName,
            Unit = item.Unit,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            DiscountAmount = item.DiscountAmount,
            TotalAmount = item.TotalAmount
        };
    }
}

public class WorkOrderResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Guid QuotationId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    
    public List<WorkOrderItemResponse> Items { get; set; } = new();

    public static WorkOrderResponse FromEntity(WorkOrder workOrder)
    {
        return new WorkOrderResponse
        {
            Id = workOrder.Id,
            Code = workOrder.Code,
            CustomerId = workOrder.CustomerId,
            QuotationId = workOrder.QuotationId,
            CreatedAt = workOrder.CreatedAt,
            CompletedAt = workOrder.CompletedAt,
            Status = workOrder.Status.ToString(),
            Notes = workOrder.Notes,
            Items = workOrder.Items.Select(WorkOrderItemResponse.FromEntity).ToList()
        };
    }
}
