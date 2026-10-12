using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using static Agendamento.IntegrationTests.Tenancy.PostgreSqlRlsTests;

namespace Agendamento.IntegrationTests.Notifications;

public sealed class AutomaticReminderPreferencePostgreSqlTests
{
    [Fact(DisplayName = "Migration exclusiva preserva clientes, habilita padrão e persiste preferência isolada por RLS @spec:AC-207")]
    public async Task UpgradeDefaultsExistingCustomersAndIsolatesPreference()
    {
        await using var fixture = await Database.Create(migrationTarget: "20261012154514_AddNotificationCustomerHistory");
        var before = (string)(await Scalar(fixture.Admin, "SELECT jsonb_agg(to_jsonb(c) ORDER BY \"Id\")::text FROM \"Customers\" c"))!;
        var options = new DbContextOptionsBuilder<AgendamentoDbContext>().UseNpgsql(fixture.MigrationConnection).Options;
        await using (var db = new AgendamentoDbContext(options, new Tenant(Guid.Empty)))
        { await db.Database.MigrateAsync(); Assert.Empty(await db.Database.GetPendingMigrationsAsync()); }
        var after = (string)(await Scalar(fixture.Admin, "SELECT jsonb_agg(to_jsonb(c)-'AutomaticRemindersEnabled' ORDER BY \"Id\")::text FROM \"Customers\" c"))!;
        Assert.Equal(before, after);
        Assert.Equal(2L, await Scalar(fixture.Admin, "SELECT count(*) FROM \"Customers\" WHERE \"AutomaticRemindersEnabled\""));
        await using (var connection = new NpgsqlConnection(fixture.RestrictedConnection))
        {
            await connection.OpenAsync(); await using var transaction = await connection.BeginTransactionAsync();
            await Execute(connection, "SELECT set_config('app.tenant_id', @id, true)", fixture.A.ToString());
            Assert.Equal(1, await Execute(connection, "UPDATE \"Customers\" SET \"AutomaticRemindersEnabled\"=false"));
            Assert.Equal(0, await Execute(connection, "UPDATE \"Customers\" SET \"AutomaticRemindersEnabled\"=false WHERE tenant_id=@id::uuid", fixture.B.ToString()));
            await transaction.CommitAsync();
        }
        // Fresh EF contexts read the committed value through the restricted role with RLS, even without query filters.
        foreach (var id in new[] { fixture.A, fixture.B })
        {
            var tenant = new Tenant(id);
            var restricted = new DbContextOptionsBuilder<AgendamentoDbContext>().UseNpgsql(fixture.RestrictedConnection)
                .AddInterceptors(new TenantTransactionInterceptor(tenant, new AdministrativeDatabaseScope()), new TenantSaveChangesInterceptor(tenant)).Options;
            await using var db = new AgendamentoDbContext(restricted, tenant);
            var customer = await db.Customers.IgnoreQueryFilters().SingleAsync();
            Assert.Equal(id, customer.TenantId); Assert.Equal(id == fixture.B, customer.AutomaticRemindersEnabled);
            if (id == fixture.A) { customer.SetAutomaticRemindersEnabled(true); await db.SaveChangesAsync(); }
        }
        Assert.Equal(2L, await Scalar(fixture.Admin, "SELECT count(*) FROM \"Customers\" WHERE \"AutomaticRemindersEnabled\""));
    }
}
