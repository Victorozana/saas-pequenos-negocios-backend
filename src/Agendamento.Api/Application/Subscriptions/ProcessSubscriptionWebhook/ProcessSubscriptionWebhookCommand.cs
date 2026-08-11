using Agendamento.Api.Application.Common;

namespace Agendamento.Api.Application.Subscriptions.ProcessSubscriptionWebhook;

public class ProcessSubscriptionWebhookCommand
{
    public string Type { get; set; } = null!;
    public string ExternalSubscriptionId { get; set; } = null!;
    public string EventId { get; set; } = null!;
    public DateTimeOffset? PeriodStart { get; set; }
    public DateTimeOffset? PeriodEnd { get; set; }
}
