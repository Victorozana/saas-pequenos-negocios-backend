using System.Text.Json.Serialization;

namespace Agendamento.Api.Application.WorkOrders.ConvertQuotationToWorkOrder;

public class ConvertQuotationToWorkOrderCommand
{
    [JsonIgnore]
    public Guid QuotationId { get; set; }
}
