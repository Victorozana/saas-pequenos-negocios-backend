using Agendamento.Api.Application.Tenancy;
using Agendamento.Domain.Common;
using Agendamento.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Agendamento.Api.Infrastructure.Persistence;

public sealed class TenantSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ITenantContext _tenantContext;

    public TenantSaveChangesInterceptor(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        EnforceTenantIsolation(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        EnforceTenantIsolation(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void EnforceTenantIsolation(DbContext? context)
    {
        if (context == null) return;

        var newTenantEntry = context.ChangeTracker.Entries<Tenant>()
            .FirstOrDefault(e => e.State == EntityState.Added);
        var isRegisteringNewTenant = newTenantEntry != null;
        var allowedTenantId = newTenantEntry?.Entity.Id 
            ?? (_tenantContext.HasTenant ? _tenantContext.TenantId : Guid.Empty);

        foreach (var entry in context.ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                if (!_tenantContext.HasTenant && !isRegisteringNewTenant)
                {
                    throw new InvalidOperationException("Não é possível salvar entidades de tenant sem um contexto de tenant ativo.");
                }

                if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
                {
                    entry.Entity.TenantId = allowedTenantId;
                }
                else if (entry.Entity.TenantId != allowedTenantId)
                {
                    throw new InvalidOperationException("Divergência de TenantId detectada durante o salvamento.");
                }
            }
            else if (entry.State == EntityState.Deleted)
            {
                if ((!_tenantContext.HasTenant && !isRegisteringNewTenant) || entry.Entity.TenantId != allowedTenantId)
                {
                    throw new InvalidOperationException("Divergência de TenantId detectada durante a exclusão.");
                }
            }
        }
    }
}
