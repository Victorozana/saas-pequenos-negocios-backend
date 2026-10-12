using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.Api.Domain.Notifications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Agendamento.Api.Features.Common;

namespace Agendamento.Api.Features.Notifications;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/notifications")
            .RequireAuthorization()
            .WithTags("Notifications");

        group.MapGet("/", async ([FromQuery] Guid? customerId, [FromServices] AgendamentoDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var notifications = await dbContext.NotificationMessages
                .Where(n => customerId == null || n.CustomerId == customerId)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new NotificationHistoryResponse(n.Id, n.Recipient, n.Channel.ToString(),
                    n.Status.ToString(), n.CreatedAt, n.SentAt, n.CustomerId, n.Audience.ToString()))
                .ToListAsync(cancellationToken);

            return Results.Ok(notifications);
        })
        .WithName("GetNotifications")
        .Produces<List<NotificationHistoryResponse>>()
        .WithSummary("Lista o histórico de notificações do tenant atual");

        group.MapPost("/send-manual", async (
            [FromBody] SendManualNotificationRequest request,
            [FromServices] AgendamentoDbContext dbContext,
            [FromServices] Agendamento.Api.Application.Notifications.INotificationService notifications,
            CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<NotificationChannel>(request.Channel, true, out var channel) || !Enum.IsDefined(channel)
                || string.IsNullOrWhiteSpace(request.Recipient) || request.Recipient.Length > 255 || string.IsNullOrWhiteSpace(request.Content))
                return Results.Problem(statusCode: 400, title: "Informe canal, destinatário e conteúdo válidos.");

            NotificationMessage notification;
            try
            {
                notification = request.CustomerId.HasValue
                    ? await notifications.QueueForCustomerAsync(request.CustomerId.Value, request.Recipient, channel, request.Content, cancellationToken)
                    : notifications.QueueStandalone(request.Recipient, channel, request.Content);
            }
            catch (ArgumentException)
            {
                return Results.Problem(statusCode: 400, title: "Cliente não encontrado na empresa atual.");
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(new NotificationQueuedResponse("Notificação enfileirada com sucesso", notification.Id));
        })
        .WithName("SendManualNotification")
        .Produces<NotificationQueuedResponse>()
        .ProducesProblem(400)
        .WithSummary("Enfileira uma notificação manual avulsa");

        group.MapPost("/schedule-reminder", async (
            [FromBody] Agendamento.Api.Application.Notifications.ScheduleAppointmentReminder.ScheduleAppointmentReminderCommand request,
            [FromServices] Agendamento.Api.Application.Notifications.ScheduleAppointmentReminder.ScheduleAppointmentReminderHandler handler,
            CancellationToken cancellationToken) =>
        {
            var success = await handler.HandleAsync(request, cancellationToken);
            if (!success)
                return Results.Problem(statusCode: 400, title: "Não foi possível agendar o lembrete. Verifique se o agendamento existe e se o cliente possui um telefone válido.");

            return Results.Ok(new MessageResponse("Lembrete enfileirado com sucesso"));
        })
        .WithName("ScheduleReminder")
        .Produces<MessageResponse>()
        .ProducesProblem(400)
        .WithSummary("Enfileira um lembrete para agendamento");

        group.MapPost("/send-quotation-notification", async (
            [FromBody] Agendamento.Api.Application.Notifications.SendQuotationNotification.SendQuotationNotificationCommand request,
            [FromServices] Agendamento.Api.Application.Notifications.SendQuotationNotification.SendQuotationNotificationHandler handler,
            CancellationToken cancellationToken) =>
        {
            var success = await handler.HandleAsync(request, cancellationToken);
            if (!success)
                return Results.Problem(statusCode: 400, title: "Não foi possível enviar a notificação do orçamento. Verifique se o orçamento existe e se o cliente possui um telefone ou e-mail válido.");

            return Results.Ok(new MessageResponse("Notificação de orçamento enfileirada com sucesso"));
        })
        .WithName("SendQuotationNotification")
        .Produces<MessageResponse>()
        .ProducesProblem(400)
        .WithSummary("Enfileira uma notificação de orçamento emitido");
    }
}

public record SendManualNotificationRequest(string Recipient, string Channel, string Content, Guid? CustomerId = null);
public record NotificationHistoryResponse(Guid Id, string Recipient, string Channel, string Status, DateTime CreatedAt, DateTime? SentAt, Guid? CustomerId, string Audience);
public record NotificationQueuedResponse(string Message, Guid NotificationId);
