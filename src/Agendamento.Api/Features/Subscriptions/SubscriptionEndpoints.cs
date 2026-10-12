using Agendamento.Api.Application.Subscriptions.ChangePlan;
using Agendamento.Api.Application.Subscriptions;
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
        var group = app.MapGroup("/api/v1/subscriptions")
            .WithTags("Subscriptions")
            .RequireAuthorization();

        group.MapGet("/current", async ([FromServices] GetTenantSubscriptionHandler handler, CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await handler.HandleAsync(new GetTenantSubscriptionQuery(), cancellationToken);
                return Results.Ok(result);
            }
            catch (SubscriptionNotFoundException) { return MissingSubscription(); }
        }).WithName("GetCurrentSubscription").Produces<TenantSubscriptionDto>().ProducesProblem(404);

        group.MapGet("/plans", async (AgendamentoDbContext dbContext) =>
        {
            var plans = await dbContext.SaasPlans.ToListAsync();
            return Results.Ok(plans);
        })
        .AllowAnonymous()
        .WithName("GetSubscriptionPlans").Produces<List<Agendamento.Domain.Subscriptions.SaasPlan>>();

        group.MapPost("/change-plan", async ([FromBody] ChangePlanCommand command, [FromServices] ChangePlanHandler handler, CancellationToken cancellationToken) =>
        {
            try
            {
                await handler.HandleAsync(command, cancellationToken);
                return Results.NoContent();
            }
            catch (SubscriptionNotFoundException) { return MissingSubscription(); }
        }).WithName("ChangeSubscriptionPlan").Produces(StatusCodes.Status204NoContent).ProducesProblem(404);

        var webhooksGroup = app.MapGroup("/api/v1/webhooks")
            .WithTags("Webhooks")
            .AllowAnonymous();

        webhooksGroup.MapPost("/billing", async ([FromBody] WebhookPayload payload, [FromServices] ProcessSubscriptionWebhookHandler handler, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(payload.Id) || string.IsNullOrWhiteSpace(payload.Type) ||
                payload.Data?.Object == null || string.IsNullOrWhiteSpace(payload.Data.Object.Id))
                return Results.Problem(statusCode: 400, title: "Evento de cobrança inválido.",
                    detail: "Informe identificadores do evento e da assinatura, tipo e dados do evento.");
            var command = new ProcessSubscriptionWebhookCommand
            {
                EventId = payload.Id,
                Type = payload.Type,
                ExternalSubscriptionId = payload.Data.Object.Id,
                PeriodStart = payload.Data.Object.CurrentPeriodStart,
                PeriodEnd = payload.Data.Object.CurrentPeriodEnd
            };

            await handler.HandleAsync(command, cancellationToken);
            return Results.Ok();
        }).WithName("ProcessBillingWebhook").Produces(200).ProducesProblem(400);
    }

    private static IResult MissingSubscription() => Results.Problem(statusCode: 404,
        title: "Assinatura ou plano não encontrado.");
}

public class WebhookPayload
{
    public string Id { get; set; } = null!; // EventId
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
