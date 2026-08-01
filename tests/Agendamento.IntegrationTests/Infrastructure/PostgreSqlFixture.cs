using DotNet.Testcontainers.Containers;
using Testcontainers.PostgreSql;
using Xunit;

namespace Agendamento.IntegrationTests.Infrastructure;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private const string FixtureMigration = """
        CREATE TABLE IF NOT EXISTS "__FixtureMigrations" (
            "MigrationId" text PRIMARY KEY,
            "AppliedAtUtc" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS "FixtureIsolationProbe" (
            "Value" text PRIMARY KEY
        );

        INSERT INTO "__FixtureMigrations" ("MigrationId")
        VALUES ('0001_fixture_infrastructure')
        ON CONFLICT ("MigrationId") DO NOTHING;
        """;

    private readonly PostgreSqlContainer _container;

    public PostgreSqlFixture()
    {
        DatabaseName = $"agendamento_tests_{Guid.NewGuid():N}";
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase(DatabaseName)
            .WithUsername("postgres")
            .WithPassword($"test_{Guid.NewGuid():N}")
            .Build();
    }

    public string DatabaseName { get; }

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        try
        {
            await _container.StartAsync();
            await ExecuteScriptAsync(FixtureMigration);
        }
        catch
        {
            await _container.DisposeAsync();
            throw;
        }
    }

    public Task<ExecResult> ExecuteScriptAsync(string script) =>
        _container.ExecScriptAsync(script).ThrowOnFailure();

    public async Task DisposeAsync() => await _container.DisposeAsync();
}
