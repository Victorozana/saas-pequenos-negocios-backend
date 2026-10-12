using Agendamento.Api.Application.Notifications;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Notifications;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Infrastructure.Notifications;

public sealed class NotificationService(AgendamentoDbContext db, ITenantContext tenant, ICommunicationGateway gateway,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task<NotificationMessage> QueueForCustomerAsync(Guid customerId, string recipient, NotificationChannel channel,
        string content, CancellationToken cancellationToken = default)
    {
        RequireTenant();
        if (customerId == Guid.Empty || !await db.Customers.AnyAsync(c => c.Id == customerId && c.TenantId == tenant.TenantId, cancellationToken))
            throw new ArgumentException("Cliente não encontrado na empresa atual.", nameof(customerId));
        return Stage(recipient, channel, content, customerId, NotificationAudience.Customer);
    }

    public NotificationMessage QueueStandalone(string recipient, NotificationChannel channel, string content) =>
        Stage(recipient, channel, content, null, NotificationAudience.Standalone);

    public async Task<NotificationMessage?> QueueAutomaticReminderAsync(Guid customerId, string recipient, NotificationChannel channel,
        string content, CancellationToken cancellationToken = default)
    {
        RequireTenant();
        var customer = await db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == tenant.TenantId, cancellationToken);
        if (customerId == Guid.Empty || customer == null)
            throw new ArgumentException("Cliente não encontrado na empresa atual.", nameof(customerId));
        if (!customer.AutomaticRemindersEnabled) return null;
        return Stage(recipient, channel, content, customerId, NotificationAudience.Customer);
    }

    public NotificationMessage QueueSystem(string recipient, NotificationChannel channel, string content) =>
        Stage(recipient, channel, content, null, NotificationAudience.System);

    // This producer commits its own outbox intent. The row lock serializes concurrent jobs;
    // the unique database key also protects the invariant against other writers/restarts.
    public async Task<NotificationMessage?> QueueOverdueReceivableReminderAsync(Guid tenantId, Guid receivableTitleId,
        DateOnly reminderDate, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || (tenant.HasTenant && tenant.TenantId != tenantId))
            throw new InvalidOperationException("Invalid reminder tenant.");
        using var administrative = db.Administrative.Enter();
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM \"ReceivableTitles\" WHERE tenant_id = {tenantId} AND \"Id\" = {receivableTitleId} FOR UPDATE", cancellationToken);

        var title = await db.ReceivableTitles.IgnoreQueryFilters().AsNoTracking().Include(r => r.Payments)
            .SingleOrDefaultAsync(r => r.TenantId == tenantId && r.Id == receivableTitleId, cancellationToken);
        if (title == null || title.CustomerId == null || title.BalanceDue <= 0
            || title.Status is Domain.Financial.TransactionStatus.Paid or Domain.Financial.TransactionStatus.Canceled
            || DateOnly.FromDateTime(title.DueDate) >= reminderDate) return null;
        var customer = await db.Customers.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(c => c.TenantId == tenantId && c.Id == title.CustomerId, cancellationToken);
        if (customer == null || !customer.AutomaticRemindersEnabled || string.IsNullOrWhiteSpace(customer.Phone)) return null;
        if (await db.NotificationMessages.IgnoreQueryFilters().AnyAsync(m => m.TenantId == tenantId
            && m.ReceivableTitleId == receivableTitleId && m.ReminderDate == reminderDate, cancellationToken)) return null;

        var content = $"Olá, {customer.Name}. Sua conta com vencimento em {title.DueDate:dd/MM/yyyy} possui saldo aberto de {title.BalanceDue.ToString("F2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))}.";
        var message = NotificationMessage.Create(tenantId, customer.Phone, NotificationChannel.WhatsApp,
            content, customer.Id, NotificationAudience.Customer);
        message.AssociateDailyReceivableReminder(title.Id, reminderDate);
        db.NotificationMessages.Add(message);
        await db.SaveChangesAsync(cancellationToken);
        if (transaction != null) await transaction.CommitAsync(cancellationToken);
        return message;
    }

    private void RequireTenant()
    {
        if (!tenant.HasTenant || tenant.TenantId == Guid.Empty) throw new InvalidOperationException("Unauthenticated tenant context.");
    }

    private NotificationMessage Stage(string recipient, NotificationChannel channel, string content, Guid? customerId, NotificationAudience audience)
    {
        RequireTenant();
        var message = NotificationMessage.Create(tenant.TenantId, recipient, channel, content, customerId, audience);
        db.NotificationMessages.Add(message);
        return message;
    }

    public async Task ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        using var administrative = db.Administrative.Enter();
        var pending = await db.NotificationMessages.IgnoreQueryFilters()
            .Where(m => m.Status == NotificationStatus.Pending && (m.NextRetryAt == null || m.NextRetryAt <= DateTime.UtcNow))
            .OrderBy(m => m.CreatedAt).Take(50).ToListAsync(cancellationToken);
        foreach (var message in pending)
        {
            try
            {
                if (await gateway.SendAsync(message, cancellationToken)) message.MarkAsSent();
                else message.MarkAsFailed("Gateway returned false (simulated error).");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception)
            {
                // Provider errors may contain credentials or payloads. Persist only a safe reason.
                logger.LogWarning("Falha no provedor para notificação {NotificationId}", message.Id);
                message.MarkAsFailed("Communication provider failed.");
            }
        }
        if (pending.Count > 0) await db.SaveChangesAsync(cancellationToken);
    }
}
