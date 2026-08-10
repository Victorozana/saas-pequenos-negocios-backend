using Agendamento.Api.Application.Subscriptions.Services;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Infrastructure.Subscriptions;

internal sealed class PlanLimitsChecker : IPlanLimitsChecker
{
    private readonly AgendamentoDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public PlanLimitsChecker(AgendamentoDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<bool> CanAddUserAsync(CancellationToken cancellationToken = default)
    {
        var limits = await GetCurrentPlanLimitsAsync(cancellationToken);
        if (limits == null) return true; // Se não tem plano definido/assinatura, permite (ou bloqueia dependendo da regra de negócios). Vou assumir que sem plano não há limite até ser exigido.

        var currentUsersCount = await _dbContext.TenantMemberships
            .IgnoreQueryFilters()
            .CountAsync(u => u.TenantId == _tenantContext.TenantId, cancellationToken);

        return currentUsersCount < limits.MaxUsers;
    }

    public async Task<bool> CanAddCustomerAsync(CancellationToken cancellationToken = default)
    {
        var limits = await GetCurrentPlanLimitsAsync(cancellationToken);
        if (limits == null) return true;

        var currentCustomersCount = await _dbContext.Customers
            .IgnoreQueryFilters()
            .CountAsync(c => c.TenantId == _tenantContext.TenantId, cancellationToken);

        return currentCustomersCount < limits.MaxCustomers;
    }

    public async Task<bool> CanAddQuotationAsync(CancellationToken cancellationToken = default)
    {
        var limits = await GetCurrentPlanLimitsAsync(cancellationToken);
        if (limits == null) return true;

        var startOfMonth = new DateTimeOffset(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);

        var currentMonthQuotationsCount = await _dbContext.Quotations
            .IgnoreQueryFilters()
            .CountAsync(q => q.TenantId == _tenantContext.TenantId && q.IssueDate >= startOfMonth.UtcDateTime, cancellationToken);

        return currentMonthQuotationsCount < limits.MaxQuotationsPerMonth;
    }

    private async Task<Agendamento.Domain.Subscriptions.PlanLimits?> GetCurrentPlanLimitsAsync(CancellationToken cancellationToken)
    {
        var subscription = await _dbContext.TenantSubscriptions
            .Include(ts => ts.Plan)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(ts => ts.TenantId == _tenantContext.TenantId, cancellationToken);

        return subscription?.Plan?.Limits;
    }
}
