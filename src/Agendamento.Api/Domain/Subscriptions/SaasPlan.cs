namespace Agendamento.Domain.Subscriptions;

public sealed class SaasPlan
{
    private SaasPlan() { } // EF Core

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public decimal Price { get; private set; }
    public string BillingCycle { get; private set; } = null!; // Monthly, Yearly
    public PlanLimits Limits { get; private set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static SaasPlan Create(
        string name,
        string description,
        decimal price,
        string billingCycle,
        PlanLimits limits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(billingCycle);
        ArgumentNullException.ThrowIfNull(limits);
        
        if (price < 0)
            throw new ArgumentOutOfRangeException(nameof(price), "Price cannot be negative.");

        return new SaasPlan
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Price = price,
            BillingCycle = billingCycle,
            Limits = limits,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }
}
