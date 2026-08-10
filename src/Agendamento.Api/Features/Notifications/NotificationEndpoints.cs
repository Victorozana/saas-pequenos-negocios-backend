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
            .RequireAuthorization()
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

        group.MapPost("/schedule-reminder", async (
            [FromBody] Agendamento.Api.Application.Notifications.ScheduleAppointmentReminder.ScheduleAppointmentReminderCommand request,
            [FromServices] Agendamento.Api.Application.Notifications.ScheduleAppointmentReminder.ScheduleAppointmentReminderHandler handler,
            CancellationToken cancellationToken) =>
        {
            var success = await handler.HandleAsync(request, cancellationToken);
            if (!success)
                return Results.BadRequest(new { Message = "Não foi possível agendar o lembrete. Verifique se o agendamento existe e se o cliente possui um telefone válido." });

            return Results.Ok(new { Message = "Lembrete enfileirado com sucesso" });
        })
        .WithName("ScheduleReminder")
        .WithSummary("Enfileira um lembrete para agendamento");

        group.MapPost("/send-quotation-notification", async (
            [FromBody] Agendamento.Api.Application.Notifications.SendQuotationNotification.SendQuotationNotificationCommand request,
            [FromServices] Agendamento.Api.Application.Notifications.SendQuotationNotification.SendQuotationNotificationHandler handler,
            CancellationToken cancellationToken) =>
        {
            var success = await handler.HandleAsync(request, cancellationToken);
            if (!success)
                return Results.BadRequest(new { Message = "Não foi possível enviar a notificação do orçamento. Verifique se o orçamento existe e se o cliente possui um telefone ou e-mail válido." });

            return Results.Ok(new { Message = "Notificação de orçamento enfileirada com sucesso" });
        })
        .WithName("SendQuotationNotification")
        .WithSummary("Enfileira uma notificação de orçamento emitido");
    }
}

public record SendManualNotificationRequest(string Recipient, string Channel, string Content);
