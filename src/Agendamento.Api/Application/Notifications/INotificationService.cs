using Agendamento.Api.Domain.Notifications;

namespace Agendamento.Api.Application.Notifications;

// Queue methods stage the outbox in the caller's DbContext. The caller commits
// together with its business operation; external delivery happens only in the worker.
public interface INotificationService
{
    Task<NotificationMessage> QueueForCustomerAsync(Guid customerId, string recipient, NotificationChannel channel, string content, CancellationToken cancellationToken = default);
    Task<NotificationMessage?> QueueAutomaticReminderAsync(Guid customerId, string recipient, NotificationChannel channel, string content, CancellationToken cancellationToken = default);
    NotificationMessage QueueStandalone(string recipient, NotificationChannel channel, string content);
    NotificationMessage QueueSystem(string recipient, NotificationChannel channel, string content);
    Task ProcessPendingAsync(CancellationToken cancellationToken = default);
    Task<NotificationMessage?> QueueOverdueReceivableReminderAsync(Guid tenantId, Guid receivableTitleId, DateOnly reminderDate, CancellationToken cancellationToken = default);
}
