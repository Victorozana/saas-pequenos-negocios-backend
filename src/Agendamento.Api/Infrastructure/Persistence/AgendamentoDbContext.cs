using Microsoft.EntityFrameworkCore;
using Agendamento.Domain.Tenants;
using Agendamento.Domain.Identity;
using System.Reflection;

namespace Agendamento.Api.Infrastructure.Persistence;

public class AgendamentoDbContext : DbContext
{
    public AgendamentoDbContext(DbContextOptions<AgendamentoDbContext> options) : base(options)
    {
    }

    public DbSet<Tenant> Tenants { get; set; } = null!;
    public DbSet<TenantFiscalProfile> TenantFiscalProfiles { get; set; } = null!;
    public DbSet<TenantMembership> TenantMemberships { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }
}
