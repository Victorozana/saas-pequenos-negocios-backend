namespace Agendamento.Domain.Subscriptions;

public record PlanLimits
{
    public int MaxUsers { get; init; }
    public int MaxCustomers { get; init; }
    public int MaxQuotationsPerMonth { get; init; }

    public PlanLimits(int maxUsers, int maxCustomers, int maxQuotationsPerMonth)
    {
        MaxUsers = maxUsers;
        MaxCustomers = maxCustomers;
        MaxQuotationsPerMonth = maxQuotationsPerMonth;
    }
}
