using Agendamento.Api.Application.Common;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Application.Subscriptions.GetTenantSubscription;

public class GetTenantSubscriptionQuery { }

public record TenantSubscriptionDto(
    Guid Id,
    Guid PlanId,
    string PlanName,
    SubscriptionStatus Status,
    DateTimeOffset CurrentPeriodStart,
    DateTimeOffset CurrentPeriodEnd,
    DateTimeOffset? TrialEndDate,
    PlanLimits Limits);

public class GetTenantSubscriptionHandler
{
    private readonly AgendamentoDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetTenantSubscriptionHandler(AgendamentoDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<TenantSubscriptionDto> HandleAsync(GetTenantSubscriptionQuery request, CancellationToken cancellationToken)
    {
        var subscription = await _dbContext.TenantSubscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.TenantId == _tenantContext.TenantId, cancellationToken);

        if (subscription == null)
            throw new Exception("Tenant não possui assinatura ativa."); // Idealmente usar uma exceção de negócio NotFoundException

        return new TenantSubscriptionDto(
            subscription.Id,
            subscription.PlanId,
            subscription.Plan.Name,
            subscription.Status,
            subscription.CurrentPeriodStart,
            subscription.CurrentPeriodEnd,
            subscription.TrialEndDate,
            subscription.Plan.Limits);
    }
}
