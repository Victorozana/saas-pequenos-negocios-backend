using Agendamento.Api.Domain.Services;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.IntegrationTests.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;
using static Agendamento.IntegrationTests.Tenancy.PostgreSqlRlsTests;

namespace Agendamento.IntegrationTests.Services;

public sealed class ServiceCatalogPersistenceTests
{
    [Fact(DisplayName = "Upgrade histórico catálogo preserva todos campos identidades preços e estados sem recriação @spec:AC-045")]
    public async Task HistoricalCatalogUpgradePreservesAllRows()
    {
        await using var fixture = await Database.Create(migrationTarget: "20261012041401_EnforceTenantRowLevelSecurity");
        await using (var db = Context(fixture, fixture.A, false))
        {
            var active = ServiceItem.Create(fixture.A, new string('ç', 200), new string('d', 1000), new string('h', 50), 9999999999999999.99m);
            var inactive = ServiceItem.Create(fixture.A, "Histórico", null, "un", .01m);
            inactive.Inactivate();
            db.AddRange(active, inactive, ServiceItem.Create(fixture.A, "Gratuito", "", "un", 0m));
            await db.SaveChangesAsync();
        }
        var before = await Scalar(fixture.Admin, "SELECT json_agg(s ORDER BY \"Id\")::text FROM \"ServiceItems\" s");
        await using (var migrator = Context(fixture, Guid.Empty, false))
        {
            await migrator.GetService<IMigrator>().MigrateAsync();
            Assert.Empty(await migrator.Database.GetPendingMigrationsAsync());
            Assert.False(migrator.Database.HasPendingModelChanges());
        }
        Assert.Equal(before, await Scalar(fixture.Admin, "SELECT json_agg(s ORDER BY \"Id\")::text FROM \"ServiceItems\" s"));
        await using var fresh = Context(fixture, fixture.A);
        Assert.Equal(4, await fresh.ServiceItems.CountAsync());
        Assert.False((await fresh.ServiceItems.SingleAsync(s => s.Name == "Histórico")).IsActive);
        Assert.Null((await fresh.ServiceItems.SingleAsync(s => s.Name == "Histórico")).Description);
        Assert.Equal(9999999999999999.99m, (await fresh.ServiceItems.SingleAsync(s => s.Name.Length == 200)).BasePrice);
    }

    private static AgendamentoDbContext Context(Database fixture, Guid id, bool restricted = true)
    {
        var tenant = new Tenant(id);
        var options = new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseNpgsql(restricted ? fixture.RestrictedConnection : fixture.MigrationConnection)
            .AddInterceptors(new TenantSaveChangesInterceptor(tenant));
        if (restricted) options.AddInterceptors(new TenantTransactionInterceptor(tenant, new AdministrativeDatabaseScope()));
        return new AgendamentoDbContext(options.Options, tenant);
    }

    [Fact(DisplayName = "Catálogo PostgreSQL preserva identidade campos limites preço zero centavos teto e inativação em contextos novos @spec:AC-045")]
    public async Task CompleteRoundtripAndUpdate()
    {
        await using var fixture = await Database.Create();
        var originals = new[]
        {
            ServiceItem.Create(fixture.A, new string('ç', 200), new string('á', 1000), new string('h', 50), 9999999999999999.99m),
            ServiceItem.Create(fixture.A, "Gratuito", null, "un", 0m),
            ServiceItem.Create(fixture.A, "Centavos", "", "hora", .01m),
            ServiceItem.Create(fixture.A, string.Concat(Enumerable.Repeat("🔧", 200)), string.Concat(Enumerable.Repeat("🔧", 1000)), string.Concat(Enumerable.Repeat("🔧", 50)), 0m)
        };
        originals[2].Inactivate();
        await using (var write = Context(fixture, fixture.A))
        {
            write.AddRange(originals);
            await write.SaveChangesAsync();
        }
        await using (var read = Context(fixture, fixture.A))
        {
            foreach (var expected in originals)
            {
                var actual = await read.ServiceItems.SingleAsync(s => s.Id == expected.Id);
                Assert.Equal(expected.TenantId, actual.TenantId);
                Assert.Equal(expected.Name, actual.Name);
                Assert.Equal(expected.Description, actual.Description);
                Assert.Equal(expected.Unit, actual.Unit);
                Assert.Equal(expected.BasePrice, actual.BasePrice);
                Assert.Equal(expected.IsActive, actual.IsActive);
            }
            var updated = await read.ServiceItems.SingleAsync(s => s.Id == originals[0].Id);
            updated.Update("Atualizado ç", null, "un", 12.34m);
            updated.Inactivate();
            await read.SaveChangesAsync();
        }
        await using var final = Context(fixture, fixture.A);
        var persisted = await final.ServiceItems.SingleAsync(s => s.Id == originals[0].Id);
        Assert.Equal(fixture.A, persisted.TenantId);
        Assert.Equal("Atualizado ç", persisted.Name);
        Assert.Null(persisted.Description);
        Assert.Equal("un", persisted.Unit);
        Assert.Equal(12.34m, persisted.BasePrice);
        Assert.False(persisted.IsActive);
    }

    [Fact(DisplayName = "Catálogo filtro EF e RLS independente isolam tenants e ausência de tenant inclusive IgnoreQueryFilters @spec:AC-045 @principle:P-004")]
    public async Task EfAndIndependentRlsIsolation()
    {
        await using var fixture = await Database.Create();
        foreach (var id in new[] { fixture.A, fixture.B, Guid.Empty })
        {
            await using (var owner = Context(fixture, id, false))
            {
                Assert.Equal(id == Guid.Empty ? 0 : 1, await owner.ServiceItems.CountAsync());
                Assert.Equal(2, await owner.ServiceItems.IgnoreQueryFilters().CountAsync());
            }
            await using var restricted = Context(fixture, id);
            var visible = await restricted.ServiceItems.IgnoreQueryFilters().ToListAsync();
            Assert.Equal(id == Guid.Empty ? 0 : 1, visible.Count);
            Assert.All(visible, row => Assert.Equal(id, row.TenantId));
        }
        // Direct SQL write/insert barriers for ServiceItems are also exercised by
        // PostgreSqlRlsTests.DirectSqlCannotCrossTenantBoundaries (all tenant tables).
        await using var sql = new NpgsqlConnection(fixture.RestrictedConnection);
        await sql.OpenAsync();
        Assert.Equal(0L, await Scalar(sql, "SELECT count(*) FROM \"ServiceItems\""));
        await using var tx = await sql.BeginTransactionAsync();
        await Execute(sql, "SELECT set_config('app.tenant_id', @id, true)", fixture.A.ToString());
        Assert.Equal(fixture.A, await Scalar(sql, "SELECT tenant_id FROM \"ServiceItems\""));
        Assert.Equal(0, await Execute(sql, "DELETE FROM \"ServiceItems\" WHERE tenant_id=@id::uuid", fixture.B.ToString()));
        await tx.RollbackAsync();
        Assert.Equal(2L, await Scalar(fixture.Admin, "SELECT count(*) FROM \"ServiceItems\""));
    }

    [Fact(DisplayName = "Constraints físicas do catálogo recusam null comprimentos excessivos overflow e tenant inexistente sem alterar linhas @spec:AC-045")]
    public async Task PhysicalRequiredLengthsNumericAndTenantForeignKey()
    {
        await using var fixture = await Database.Create();
        var before = await Scalar(fixture.Admin, "SELECT json_agg(s ORDER BY \"Id\")::text FROM \"ServiceItems\" s");
        // Owner bypasses RLS so WITH CHECK cannot mask NOT NULL or the tenant FK.
        await using var tx = await fixture.Admin.BeginTransactionAsync();
        foreach (var (assignment, state) in new[]
        {
            ("\"Name\"=NULL", PostgresErrorCodes.NotNullViolation),
            ("\"Unit\"=NULL", PostgresErrorCodes.NotNullViolation),
            ("\"BasePrice\"=NULL", PostgresErrorCodes.NotNullViolation),
            ("\"IsActive\"=NULL", PostgresErrorCodes.NotNullViolation),
            ("tenant_id=NULL", PostgresErrorCodes.NotNullViolation),
            ("\"Name\"=repeat('n',201)", PostgresErrorCodes.StringDataRightTruncation),
            ("\"Description\"=repeat('d',1001)", PostgresErrorCodes.StringDataRightTruncation),
            ("\"Unit\"=repeat('u',51)", PostgresErrorCodes.StringDataRightTruncation),
            ("\"BasePrice\"=10000000000000000.00", PostgresErrorCodes.NumericValueOutOfRange),
            ($"tenant_id='{Guid.NewGuid()}'::uuid", PostgresErrorCodes.ForeignKeyViolation)
        })
        {
            await Execute(fixture.Admin, "SAVEPOINT invalid_catalog");
            var error = await Assert.ThrowsAsync<PostgresException>(() => Execute(fixture.Admin,
                $"UPDATE \"ServiceItems\" SET {assignment} WHERE tenant_id=@id::uuid", fixture.A.ToString()));
            Assert.Equal(state, error.SqlState);
            await Execute(fixture.Admin, "ROLLBACK TO SAVEPOINT invalid_catalog");
            Assert.Equal(before, await Scalar(fixture.Admin, "SELECT json_agg(s ORDER BY \"Id\")::text FROM \"ServiceItems\" s"));
        }
        Assert.Equal("numeric(18,2)", await Scalar(fixture.Admin,
            "SELECT format_type(atttypid,atttypmod) FROM pg_attribute WHERE attrelid='\"ServiceItems\"'::regclass AND attname='BasePrice'"));
        await tx.RollbackAsync();
    }

    [Fact(DisplayName = "Inativar catálogo preserva vínculos e valores históricos de orçamento e OS e Restrict impede exclusão @spec:AC-045")]
    public async Task InactivationPreservesHistoricalLinksAndRestrict()
    {
        await using var fixture = await Database.Create();
        var service = (Guid)(await Scalar(fixture.Admin, "SELECT \"Id\" FROM \"ServiceItems\" WHERE tenant_id=@id::uuid", fixture.A.ToString()))!;
        foreach (var table in new[] { "QuotationItem", "WorkOrderItems" })
            await Execute(fixture.Admin, $"UPDATE \"{table}\" SET \"ServiceItemId\"=@id::uuid WHERE tenant_id='{fixture.A}'::uuid", service.ToString());
        async Task<string[]> Rows() => [
            (string)(await Scalar(fixture.Admin, "SELECT json_agg(q ORDER BY \"Id\")::text FROM \"Quotations\" q"))!,
            (string)(await Scalar(fixture.Admin, "SELECT json_agg(w ORDER BY \"Id\")::text FROM \"WorkOrders\" w"))!,
            (string)(await Scalar(fixture.Admin, "SELECT json_agg(i ORDER BY \"Id\")::text FROM \"QuotationItem\" i"))!,
            (string)(await Scalar(fixture.Admin, "SELECT json_agg(i ORDER BY \"Id\")::text FROM \"WorkOrderItems\" i"))!];
        var before = await Rows();
        await using (var db = Context(fixture, fixture.A))
        {
            var item = await db.ServiceItems.SingleAsync(s => s.Id == service);
            item.Update("Novo nome", "Novo texto", "nova unidade", 55.55m);
            item.Inactivate();
            await db.SaveChangesAsync();
        }
        Assert.Equal(before, await Rows());
        await using (var fresh = Context(fixture, fixture.A))
        {
            Assert.False((await fresh.ServiceItems.SingleAsync(s => s.Id == service)).IsActive);
            Assert.Equal(service, (await fresh.Quotations.Include(q => q.Items).SingleAsync()).Items.Single().ServiceItemId);
            Assert.Equal(service, (await fresh.WorkOrders.Include(w => w.Items).SingleAsync()).Items.Single().ServiceItemId);
            var failure = await Assert.ThrowsAsync<PostgresException>(() => fresh.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"ServiceItems\" WHERE \"Id\"={service}"));
            Assert.Equal(PostgresErrorCodes.RestrictViolation, failure.SqlState);
        }
        // Prove each relationship independently; one FK must not mask the other.
        await using var tx = await fixture.Admin.BeginTransactionAsync();
        foreach (var unlinked in new[] { "QuotationItem", "WorkOrderItems" })
        {
            await Execute(fixture.Admin, "SAVEPOINT restrict_catalog");
            await Execute(fixture.Admin, $"UPDATE \"{unlinked}\" SET \"ServiceItemId\"=NULL WHERE tenant_id=@id::uuid", fixture.A.ToString());
            var error = await Assert.ThrowsAsync<PostgresException>(() => Execute(fixture.Admin, "DELETE FROM \"ServiceItems\" WHERE \"Id\"=@id::uuid", service.ToString()));
            Assert.Equal(PostgresErrorCodes.RestrictViolation, error.SqlState);
            await Execute(fixture.Admin, "ROLLBACK TO SAVEPOINT restrict_catalog");
        }
        await tx.RollbackAsync();
        Assert.Equal(before, await Rows());
    }
}
