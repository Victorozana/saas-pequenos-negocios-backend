using Microsoft.EntityFrameworkCore;
using Agendamento.Domain.Tenants;
using Agendamento.Domain.Identity;
using System.Reflection;

namespace Agendamento.Api.Infrastructure.Persistence;

public class AgendamentoDbContext : DbContext
{
    private readonly Agendamento.Api.Application.Tenancy.ITenantContext _tenantContext;

    public AgendamentoDbContext(DbContextOptions<AgendamentoDbContext> options, Agendamento.Api.Application.Tenancy.ITenantContext tenantContext) : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants { get; set; } = null!;
    public DbSet<TenantFiscalProfile> TenantFiscalProfiles { get; set; } = null!;
    public DbSet<TenantMembership> TenantMemberships { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; } = null!;
    public DbSet<Agendamento.Api.Domain.Customers.Customer> Customers => Set<Agendamento.Api.Domain.Customers.Customer>();
    public DbSet<Agendamento.Api.Domain.Services.ServiceItem> ServiceItems => Set<Agendamento.Api.Domain.Services.ServiceItem>();
    public DbSet<Agendamento.Api.Domain.Quotations.Quotation> Quotations => Set<Agendamento.Api.Domain.Quotations.Quotation>();
    public DbSet<Agendamento.Api.Domain.WorkOrders.WorkOrder> WorkOrders => Set<Agendamento.Api.Domain.WorkOrders.WorkOrder>();
    public DbSet<Agendamento.Api.Domain.Appointments.Appointment> Appointments => Set<Agendamento.Api.Domain.Appointments.Appointment>();
    public DbSet<Agendamento.Api.Domain.Financial.ReceivableTitle> ReceivableTitles => Set<Agendamento.Api.Domain.Financial.ReceivableTitle>();
    public DbSet<Agendamento.Api.Domain.Financial.PayableTitle> PayableTitles => Set<Agendamento.Api.Domain.Financial.PayableTitle>();
    public DbSet<Agendamento.Api.Domain.Notifications.NotificationMessage> NotificationMessages => Set<Agendamento.Api.Domain.Notifications.NotificationMessage>();
    public DbSet<Agendamento.Domain.Subscriptions.SaasPlan> SaasPlans => Set<Agendamento.Domain.Subscriptions.SaasPlan>();
    public DbSet<Agendamento.Domain.Subscriptions.TenantSubscription> TenantSubscriptions => Set<Agendamento.Domain.Subscriptions.TenantSubscription>();
    public DbSet<TeamInvitation> TeamInvitations => Set<TeamInvitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(Agendamento.Domain.Common.ITenantOwned).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(AgendamentoDbContext).GetMethod(nameof(ConfigureTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)?.MakeGenericMethod(entityType.ClrType);
                method?.Invoke(this, new object[] { modelBuilder });
            }
        }

        base.OnModelCreating(modelBuilder);
    }

    private void ConfigureTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, Agendamento.Domain.Common.ITenantOwned
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => _tenantContext.HasTenant && e.TenantId == _tenantContext.TenantId);
    }
}
