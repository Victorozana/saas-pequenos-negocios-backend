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

        // Table-split deposit updates may leave the quotation itself Unchanged.
        // Validate its tracked owner rather than treating that as an unguarded write.
        foreach (var entry in context.ChangeTracker.Entries<Agendamento.Api.Domain.Quotations.DepositInfo>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            var ownership = entry.Metadata.FindOwnership()!;
            var quotationId = (Guid)entry.Property(ownership.Properties.Single().Name).CurrentValue!;
            var owner = context.ChangeTracker.Entries<Agendamento.Api.Domain.Quotations.Quotation>()
                .SingleOrDefault(e => e.Entity.Id == quotationId);
            if ((!_tenantContext.HasTenant && !isRegisteringNewTenant) || owner == null
                || owner.Entity.TenantId != allowedTenantId
                || (owner.State != EntityState.Added
                    && owner.Property(e => e.TenantId).OriginalValue != allowedTenantId))
                throw new InvalidOperationException("Divergência de TenantId do sinal de orçamento detectada durante o salvamento.");
        }

        // Quotation items can be tracked independently of their aggregate. Their
        // shadow tenant must receive the same write barrier as the owner.
        foreach (var entry in context.ChangeTracker.Entries<Agendamento.Api.Domain.Quotations.QuotationItem>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            var tenant = entry.Property<Guid>("TenantId");
            if ((!_tenantContext.HasTenant && !isRegisteringNewTenant)
                || (entry.State != EntityState.Added && tenant.OriginalValue != allowedTenantId)
                || (tenant.CurrentValue != Guid.Empty && tenant.CurrentValue != allowedTenantId)
                || (entry.State != EntityState.Added && tenant.CurrentValue != allowedTenantId))
                throw new InvalidOperationException("Divergência de TenantId do item de orçamento detectada durante o salvamento.");
        }

        foreach (var entry in context.ChangeTracker.Entries().Where(e =>
            e.Metadata.FindProperty("TenantId")?.IsShadowProperty() == true && e.State == EntityState.Added))
            entry.Property("TenantId").CurrentValue = allowedTenantId;

        foreach (var entry in context.ChangeTracker.Entries<ITenantOwned>())
        {
            if (!_tenantContext.HasTenant && context is AgendamentoDbContext db && db.Administrative.IsActive
                && entry.Entity is Agendamento.Api.Domain.Notifications.NotificationMessage)
                continue; // Trusted background outbox only; ordinary tenant writes retain the guard.
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                var originalTenantId = entry.Property(nameof(ITenantOwned.TenantId)).OriginalValue;
                if (originalTenantId is not Guid tenantId || tenantId != allowedTenantId)
                {
                    throw new InvalidOperationException("Divergência do TenantId original detectada durante o salvamento.");
                }
            }

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
