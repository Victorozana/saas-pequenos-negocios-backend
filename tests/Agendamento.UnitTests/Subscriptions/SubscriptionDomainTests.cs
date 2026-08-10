using Agendamento.Domain.Subscriptions;
using Xunit;

namespace Agendamento.UnitTests.Subscriptions;

public class SubscriptionDomainTests
{
    [Fact]
    public void CreateTrial_Should_SetStatusToTrialingAnd14DaysDuration()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        // Act
        var subscription = TenantSubscription.CreateTrial(tenantId, planId);

        // Assert
        Assert.Equal(SubscriptionStatus.Trialing, subscription.Status);
        
        var expectedEndDate = DateTimeOffset.UtcNow.AddDays(14);
        Assert.True(Math.Abs((subscription.CurrentPeriodEnd - expectedEndDate).TotalSeconds) < 10, "CurrentPeriodEnd not within 10 seconds");
        Assert.True(subscription.TrialEndDate.HasValue && Math.Abs((subscription.TrialEndDate.Value - expectedEndDate).TotalSeconds) < 10, "TrialEndDate not within 10 seconds");
    }

    [Fact]
    public void MarkAsActive_Should_UpdateStatusAndPeriod()
    {
        // Arrange
        var subscription = TenantSubscription.CreateTrial(Guid.NewGuid(), Guid.NewGuid());
        var start = DateTimeOffset.UtcNow;
        var end = start.AddMonths(1);

        // Act
        subscription.MarkAsActive(start, end);

        // Assert
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.Equal(start, subscription.CurrentPeriodStart);
        Assert.Equal(end, subscription.CurrentPeriodEnd);
    }
}
