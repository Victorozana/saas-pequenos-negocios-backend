using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.Api.Domain.Notifications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Features.Notifications;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/notifications")
            .RequireAuthorization("TenantPolicy")
            .WithTags("Notifications");

        group.MapGet("/", async ([FromServices] AgendamentoDbContext dbContext, [FromServices] ITenantContext tenantContext, CancellationToken cancellationToken) =>
        {
            var notifications = await dbContext.NotificationMessages
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new
                {
                    n.Id,
                    n.Recipient,
                    Channel = n.Channel.ToString(),
                    Status = n.Status.ToString(),
                    n.CreatedAt,
                    n.SentAt
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(notifications);
        })
        .WithName("GetNotifications")
        .WithSummary("Lista o histórico de notificações do tenant atual");

        group.MapPost("/send-manual", async (
            [FromBody] SendManualNotificationRequest request,
            [FromServices] AgendamentoDbContext dbContext,
            [FromServices] ITenantContext tenantContext,
            CancellationToken cancellationToken) =>
        {
            var channel = Enum.Parse<NotificationChannel>(request.Channel, ignoreCase: true);

            var notification = NotificationMessage.Create(
                tenantContext.TenantId,
                request.Recipient,
                channel,
                request.Content);

            dbContext.NotificationMessages.Add(notification);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(new { Message = "Notificação enfileirada com sucesso", NotificationId = notification.Id });
        })
        .WithName("SendManualNotification")
        .WithSummary("Enfileira uma notificação manual avulsa");
    }
}

public record SendManualNotificationRequest(string Recipient, string Channel, string Content);
