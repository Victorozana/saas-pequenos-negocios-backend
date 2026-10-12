using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.Domain.Common;
using Agendamento.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace Agendamento.ArchitectureTests.Tenancy;

public sealed class TenantOwnedMigrationTests
{
    private sealed class NoTenant : ITenantContext { public Guid TenantId => Guid.Empty; public bool HasTenant => false; }

    [Fact(DisplayName = "Modelo e migration descoberta protegem tabelas de tenant e dependentes @spec:AC-035")]
    public void TenantTablesCannotOmitProtection()
    {
        // No connection is opened and no credential is used by this architectural check.
        var options = new DbContextOptionsBuilder<AgendamentoDbContext>().UseNpgsql("Host=localhost;Database=architecture_only").Options;
        using var db = new AgendamentoDbContext(options, new NoTenant());
        var model = db.GetService<IDesignTimeModel>().Model;
        var entities = model.GetEntityTypes().Where(e => typeof(ITenantOwned).IsAssignableFrom(e.ClrType)
            || e.GetTableName() is "QuotationItem" or "WorkOrderItems" or "ReceivablePayments" or "PayablePayments").ToArray();
        var migrations = db.GetService<IMigrationsAssembly>();
        var migrationType = migrations.Migrations.Single(p => p.Key.EndsWith("_EnforceTenantRowLevelSecurity")).Value;
        var migration = migrations.CreateMigration(migrationType, db.Database.ProviderName!);
        var sql = string.Join('\n', migration.UpOperations.OfType<SqlOperation>().Select(o => o.Sql));
        Assert.NotEmpty(entities);
        foreach (var entity in entities)
        {
            var property = entity.FindProperty("TenantId");
            Assert.NotNull(property);
            Assert.False(property.IsNullable);
            Assert.Equal("tenant_id", property.GetColumnName());
            Assert.Contains(entity.GetForeignKeys(), f => f.PrincipalEntityType.ClrType == typeof(Tenant) && f.Properties.Contains(property));
            Assert.Contains(entity.GetIndexes(), i => i.Properties[0] == property);
            var table = entity.GetTableName();
            Assert.Contains($"ALTER TABLE \"{table}\" ENABLE ROW LEVEL SECURITY", sql);
            Assert.Contains($"ALTER TABLE \"{table}\" FORCE ROW LEVEL SECURITY", sql);
            Assert.Contains($"CREATE POLICY tenant_isolation ON \"{table}\"", sql);
        }
        // Every separately stored dependent of a tenant aggregate must carry the same barrier.
        foreach (var entity in model.GetEntityTypes())
        foreach (var fk in entity.GetForeignKeys())
            if (fk.PrincipalEntityType.FindProperty("TenantId") != null && entity.GetTableName() != fk.PrincipalEntityType.GetTableName())
                Assert.NotNull(entity.FindProperty("TenantId"));
    }
}
