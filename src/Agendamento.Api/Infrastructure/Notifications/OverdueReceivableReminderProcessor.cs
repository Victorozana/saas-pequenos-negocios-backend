using Agendamento.Api.Application.Notifications;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Financial;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Infrastructure.Notifications;

public sealed class OverdueReceivableReminderProcessor(AgendamentoDbContext db, ITenantContext tenant,
    INotificationService notifications, TimeProvider clock)
{
    public async Task ProcessAsync(CancellationToken cancellationToken = default)
    {
        // Finance stores UTC dates and uses UTC calendar days, not elapsed 24-hour periods.
        var day = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var cutoff = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        using var administrative = db.Administrative.Enter();
        var titles = await db.ReceivableTitles.IgnoreQueryFilters().AsNoTracking()
            .Where(r => (!tenant.HasTenant || r.TenantId == tenant.TenantId) && r.CustomerId != null
                && r.DueDate < cutoff && r.Status != TransactionStatus.Paid && r.Status != TransactionStatus.Canceled)
            .Select(r => new { r.TenantId, r.Id }).ToListAsync(cancellationToken);
        foreach (var title in titles)
            await notifications.QueueOverdueReceivableReminderAsync(title.TenantId, title.Id, day, cancellationToken);
    }
}
