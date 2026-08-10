using Agendamento.Api.Application.Subscriptions.ChangePlan;
using Agendamento.Api.Application.Subscriptions.GetTenantSubscription;
using Agendamento.Api.Application.Subscriptions.ProcessSubscriptionWebhook;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Features.Subscriptions;

public static class SubscriptionEndpoints
{
    public static void MapSubscriptionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/subscriptions").RequireAuthorization();

        group.MapGet("/current", async ([FromServices] GetTenantSubscriptionHandler handler, CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetTenantSubscriptionQuery(), cancellationToken);
            return Results.Ok(result);
        });

        group.MapGet("/plans", async (AgendamentoDbContext dbContext) =>
        {
            var plans = await dbContext.SaasPlans.ToListAsync();
            return Results.Ok(plans);
        }).AllowAnonymous(); // Permite consultar planos sem estar logado, ideal para página de preços

        group.MapPost("/change-plan", async ([FromBody] ChangePlanCommand command, [FromServices] ChangePlanHandler handler, CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(command, cancellationToken);
            return Results.NoContent();
        });

        var webhooksGroup = app.MapGroup("/api/v1/webhooks").AllowAnonymous();

        webhooksGroup.MapPost("/billing", async ([FromBody] WebhookPayload payload, [FromServices] ProcessSubscriptionWebhookHandler handler, CancellationToken cancellationToken) =>
        {
            var command = new ProcessSubscriptionWebhookCommand
            {
                Type = payload.Type,
                ExternalSubscriptionId = payload.Data.Object.Id,
                PeriodStart = payload.Data.Object.CurrentPeriodStart,
                PeriodEnd = payload.Data.Object.CurrentPeriodEnd
            };

            await handler.HandleAsync(command, cancellationToken);
            return Results.Ok();
        });
    }
}

public class WebhookPayload
{
    public string Type { get; set; } = null!;
    public WebhookData Data { get; set; } = null!;
}

public class WebhookData
{
    public WebhookObject Object { get; set; } = null!;
}

public class WebhookObject
{
    public string Id { get; set; } = null!; // ExternalSubscriptionId
    public DateTimeOffset? CurrentPeriodStart { get; set; }
    public DateTimeOffset? CurrentPeriodEnd { get; set; }
}
