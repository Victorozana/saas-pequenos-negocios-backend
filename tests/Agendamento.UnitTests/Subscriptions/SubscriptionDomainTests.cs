using System;
using System.Threading;
using System.Threading.Tasks;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.Api.Infrastructure.Subscriptions;
using Agendamento.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Agendamento.UnitTests.Subscriptions;

public class SubscriptionDomainTests
{
    [Fact(DisplayName = "Assinatura isolada por tenant @spec:AC-070")]
    public void Subscription_Should_BelongToTenant_AC070()
    {
        var tenantId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var subscription = TenantSubscription.CreateTrial(tenantId, planId);

        Assert.Equal(tenantId, subscription.TenantId);
    }

    [Fact(DisplayName = "Acesso de escrita bloqueado se PastDue e expirou carência @spec:AC-073")]
    public void IsWriteAccessBlocked_Should_ReturnTrue_IfPastDueAndGraceExpired_AC073()
    {
        var subscription = TenantSubscription.CreateTrial(Guid.NewGuid(), Guid.NewGuid());
        var periodStart = DateTimeOffset.UtcNow.AddMonths(-1);
        var periodEnd = DateTimeOffset.UtcNow.AddDays(-5); // 5 dias atrás

        subscription.MarkAsActive(periodStart, periodEnd);
        subscription.MarkAsPastDue();

        // Check com carência de 3 dias, já passou 5
        var isBlocked = subscription.IsWriteAccessBlocked(DateTimeOffset.UtcNow, graceDays: 3);
        
        Assert.True(isBlocked);
    }

    [Fact(DisplayName = "Acesso de escrita não bloqueado se PastDue dentro da carência @spec:AC-073")]
    public void IsWriteAccessBlocked_Should_ReturnFalse_IfPastDueAndInsideGrace_AC073()
    {
        var subscription = TenantSubscription.CreateTrial(Guid.NewGuid(), Guid.NewGuid());
        var periodStart = DateTimeOffset.UtcNow.AddMonths(-1);
        var periodEnd = DateTimeOffset.UtcNow.AddDays(-1); // Venceu ontem

        subscription.MarkAsActive(periodStart, periodEnd);
        subscription.MarkAsPastDue();

        // Check com carência de 3 dias
        var isBlocked = subscription.IsWriteAccessBlocked(DateTimeOffset.UtcNow, graceDays: 3);
        
        Assert.False(isBlocked);
    }

    [Fact(DisplayName = "Trial expira após 14 dias @spec:AC-074")]
    public void IsTrialExpired_Should_ReturnTrue_After14Days_AC074()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var subscription = TenantSubscription.CreateTrial(tenantId, planId, trialDays: 14);

        // Act & Assert
        var nowBefore14Days = DateTimeOffset.UtcNow.AddDays(13);
        var nowAfter14Days = DateTimeOffset.UtcNow.AddDays(15);

        Assert.False(subscription.IsTrialExpired(nowBefore14Days));
        Assert.True(subscription.IsTrialExpired(nowAfter14Days));
        Assert.True(subscription.IsWriteAccessBlocked(nowAfter14Days)); // Acesso de escrita bloqueia após trial sem upgrade
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

    private class DummyTenantContext : ITenantContext
    {
        public DummyTenantContext(Guid tenantId)
        {
            TenantId = tenantId;
            HasTenant = true;
        }

        public Guid TenantId { get; }
        public bool HasTenant { get; }
    }

    [Fact(DisplayName = "Validação de limites (PlanLimitsChecker) @spec:AC-071")]
    public async Task PlanLimitsChecker_ShouldEnforceLimits_AC071()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        
        var options = new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var context = new AgendamentoDbContext(options, new DummyTenantContext(tenantId));
        context.Database.EnsureCreated();

        var checker = new PlanLimitsChecker(context, new DummyTenantContext(tenantId));
        
        // Testando funcionalidade: tenant limpo permite adicionar.
        var canAdd = await checker.CanAddUserAsync();
        Assert.True(canAdd);
    }
}
