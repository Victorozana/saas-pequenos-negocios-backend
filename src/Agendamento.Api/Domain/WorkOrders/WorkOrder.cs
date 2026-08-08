using Agendamento.Domain.Common;
using Agendamento.Api.Domain.Customers;
using Agendamento.Api.Domain.Quotations;

namespace Agendamento.Api.Domain.WorkOrders;

public class WorkOrder : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; set; }
    
    public string Code { get; private set; } = string.Empty;
    public Guid CustomerId { get; private set; }
    public Customer? Customer { get; private set; }
    
    public Guid QuotationId { get; private set; }
    public Quotation? Quotation { get; private set; }
    
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    
    public WorkOrderStatus Status { get; private set; }
    public string? Notes { get; private set; }
    
    private readonly List<WorkOrderItem> _items = new();
    public IReadOnlyCollection<WorkOrderItem> Items => _items.AsReadOnly();

    private WorkOrder() { } // EF Core

    public static WorkOrder CreateFromQuotation(Quotation quotation)
    {
        if (quotation.Status != QuotationStatus.Approved)
            throw new InvalidOperationException("Can only create a work order from an approved quotation.");

        var workOrder = new WorkOrder
        {
            Id = Guid.NewGuid(),
            TenantId = quotation.TenantId,
            Code = $"OS-{quotation.Code}",
            CustomerId = quotation.CustomerId,
            QuotationId = quotation.Id,
            CreatedAt = DateTime.UtcNow,
            Status = WorkOrderStatus.Scheduled,
            Notes = quotation.Notes
        };

        foreach (var item in quotation.Items)
        {
            workOrder._items.Add(WorkOrderItem.Create(
                item.ServiceItemId,
                item.ServiceName,
                item.Unit,
                item.Quantity,
                item.UnitPrice,
                item.DiscountAmount
            ));
        }

        return workOrder;
    }

    public void UpdateStatus(WorkOrderStatus newStatus, string? newNotes = null)
    {
        if (Status == WorkOrderStatus.Completed || Status == WorkOrderStatus.Cancelled)
            throw new InvalidOperationException("Cannot update status of a completed or cancelled work order.");

        Status = newStatus;
        
        if (newNotes != null)
            Notes = newNotes;

        if (newStatus == WorkOrderStatus.Completed)
            CompletedAt = DateTime.UtcNow;
    }
}
