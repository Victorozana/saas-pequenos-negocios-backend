using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace Agendamento.IntegrationTests.Persistence;

public sealed class LocalPostgreSqlIsolationTests
{
    private sealed class NoTenant : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
        public bool HasTenant => false;
    }

    [Fact(DisplayName = "PostgreSQL real aplica migrations em bases exclusivas sem compartilhar dados @spec:AC-004")]
    public async Task MigrationsAndDataAreIsolatedBetweenRuns()
    {
        var configuration = new ConfigurationBuilder().AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables().Build();
        var connection = configuration.GetConnectionString("PostgreSqlMigration")
            ?? configuration.GetConnectionString("PostgreSql")
            ?? throw new InvalidOperationException("Configure a conexão local para testar PostgreSQL.");
        var builder = new NpgsqlConnectionStringBuilder(connection) { Database = "postgres", IncludeErrorDetail = false };
        await using var admin = new NpgsqlConnection(builder.ConnectionString);
        await admin.OpenAsync();
        var created = new List<string>();
        try
        {
            for (var run = 0; run < 2; run++)
            {
                var database = "agendamento_isolation_" + Guid.NewGuid().ToString("N");
                await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin))
                    await create.ExecuteNonQueryAsync();
                created.Add(database);
                builder.Database = database;
                var options = new DbContextOptionsBuilder<AgendamentoDbContext>().UseNpgsql(builder.ConnectionString).Options;
                await using var db = new AgendamentoDbContext(options, new NoTenant());
                await db.Database.MigrateAsync();
                Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
                Assert.Empty(await db.Database.GetPendingMigrationsAsync());
                await using var isolated = new NpgsqlConnection(builder.ConnectionString);
                await isolated.OpenAsync();
                await using var version = new NpgsqlCommand("SELECT current_setting('server_version_num')::integer", isolated);
                Assert.True((int)(await version.ExecuteScalarAsync())! >= 160000);
                await using var probe = new NpgsqlCommand("SELECT to_regclass('public.__isolation_probe') IS NULL", isolated);
                Assert.True((bool)(await probe.ExecuteScalarAsync())!);
                await using var marker = new NpgsqlCommand("CREATE TABLE __isolation_probe (value integer); INSERT INTO __isolation_probe VALUES (1)", isolated);
                await marker.ExecuteNonQueryAsync();
            }
            Assert.Equal(2, created.Distinct().Count());
        }
        finally
        {
            foreach (var database in created)
            {
                // Limpar somente o pool desta base temporária; outros testes provam reutilização de conexão.
                builder.Database = database;
                await using var pool = new NpgsqlConnection(builder.ConnectionString);
                NpgsqlConnection.ClearPool(pool);
                // Somente bases exclusivas criadas por esta execução são removidas.
                await using var drop = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", admin);
                await drop.ExecuteNonQueryAsync();
            }
        }
    }
}
