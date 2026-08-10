using Agendamento.Api.Application.Common;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Application.Subscriptions.ProcessSubscriptionWebhook;

public class ProcessSubscriptionWebhookHandler
{
    private readonly AgendamentoDbContext _dbContext;

    public ProcessSubscriptionWebhookHandler(AgendamentoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task HandleAsync(ProcessSubscriptionWebhookCommand request, CancellationToken cancellationToken)
    {
        var subscription = await _dbContext.TenantSubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.ExternalSubscriptionId == request.ExternalSubscriptionId, cancellationToken);

        if (subscription == null)
        {
            // Webhook recebido para uma assinatura desconhecida. Ignora ou loga.
            return;
        }

        switch (request.Type)
        {
            case "invoice.paid":
                if (request.PeriodStart.HasValue && request.PeriodEnd.HasValue)
                {
                    subscription.MarkAsActive(request.PeriodStart.Value, request.PeriodEnd.Value);
                }
                break;

            case "invoice.payment_failed":
                subscription.MarkAsPastDue();
                break;

            case "customer.subscription.deleted":
            case "customer.subscription.canceled":
                subscription.Cancel();
                break;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
