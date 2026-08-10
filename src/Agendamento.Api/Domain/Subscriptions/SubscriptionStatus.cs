namespace Agendamento.Domain.Subscriptions;

public enum SubscriptionStatus
{
    Trialing = 1,
    Active = 2,
    PastDue = 3,
    Canceled = 4,
    Incomplete = 5,
    IncompleteExpired = 6
}
