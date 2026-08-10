using Agendamento.Domain.Common;

namespace Agendamento.Domain.Subscriptions;

public sealed class TenantSubscription : ITenantOwned
{
    private TenantSubscription() { } // EF Core

    public Guid Id { get; private set; }
    public Guid TenantId { get; set; }
    public Guid PlanId { get; private set; }
    public SaasPlan Plan { get; private set; } = null!;
    public SubscriptionStatus Status { get; private set; }
    public string? ExternalSubscriptionId { get; private set; }
    public DateTimeOffset CurrentPeriodStart { get; private set; }
    public DateTimeOffset CurrentPeriodEnd { get; private set; }
    public DateTimeOffset? TrialEndDate { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public static TenantSubscription CreateTrial(Guid tenantId, Guid planId, int trialDays = 14)
    {
        var now = DateTimeOffset.UtcNow;
        return new TenantSubscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PlanId = planId,
            Status = SubscriptionStatus.Trialing,
            CurrentPeriodStart = now,
            CurrentPeriodEnd = now.AddDays(trialDays),
            TrialEndDate = now.AddDays(trialDays),
            CreatedAtUtc = now
        };
    }

    public void ChangePlan(Guid newPlanId)
    {
        PlanId = newPlanId;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void UpdateExternalSubscription(string externalSubscriptionId)
    {
        ExternalSubscriptionId = externalSubscriptionId;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkAsActive(DateTimeOffset periodStart, DateTimeOffset periodEnd)
    {
        Status = SubscriptionStatus.Active;
        CurrentPeriodStart = periodStart;
        CurrentPeriodEnd = periodEnd;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkAsPastDue()
    {
        Status = SubscriptionStatus.PastDue;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Cancel()
    {
        Status = SubscriptionStatus.Canceled;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
