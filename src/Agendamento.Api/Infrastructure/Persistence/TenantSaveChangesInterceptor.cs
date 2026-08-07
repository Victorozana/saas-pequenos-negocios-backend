using Agendamento.Api.Application.Tenancy;
using Agendamento.Domain.Common;
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

        foreach (var entry in context.ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                if (!_tenantContext.HasTenant)
                {
                    throw new InvalidOperationException("Não é possível salvar entidades de tenant sem um contexto de tenant ativo.");
                }

                if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
                {
                    entry.Entity.TenantId = _tenantContext.TenantId;
                }
                else if (entry.Entity.TenantId != _tenantContext.TenantId)
                {
                    throw new InvalidOperationException("Divergência de TenantId detectada durante o salvamento.");
                }
            }
            else if (entry.State == EntityState.Deleted)
            {
                if (!_tenantContext.HasTenant || entry.Entity.TenantId != _tenantContext.TenantId)
                {
                    throw new InvalidOperationException("Divergência de TenantId detectada durante a exclusão.");
                }
            }
        }
    }
}
