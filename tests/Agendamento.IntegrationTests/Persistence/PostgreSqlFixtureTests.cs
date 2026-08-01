using Agendamento.IntegrationTests.Infrastructure;
using Xunit;

namespace Agendamento.IntegrationTests.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class PostgreSqlFixtureTests(PostgreSqlFixture fixture)
{
    [Fact(DisplayName = "PostgreSQL real aplica a migration da fixture @spec:AC-004")]
    public async Task RealPostgreSqlAppliesTheFixtureMigration()
    {
        var result = await fixture.ExecuteScriptAsync("""
            DO $$
            BEGIN
                IF current_setting('server_version_num')::integer < 160000 THEN
                    RAISE EXCEPTION 'A fixture requer PostgreSQL 16 ou superior.';
                END IF;

                IF to_regclass('public."FixtureIsolationProbe"') IS NULL THEN
                    RAISE EXCEPTION 'A tabela criada pela migration não existe.';
                END IF;

                IF NOT EXISTS (
                    SELECT 1
                    FROM "__FixtureMigrations"
                    WHERE "MigrationId" = '0001_fixture_infrastructure'
                ) THEN
                    RAISE EXCEPTION 'A migration da fixture não foi registrada.';
                END IF;
            END $$;
            """);

        Assert.Equal(0, result.ExitCode);
    }

    [Fact(DisplayName = "Execuções de persistência não compartilham dados @spec:AC-004")]
    public async Task PersistenceRunsDoNotShareData()
    {
        var marker = Guid.NewGuid().ToString("N");
        var insertResult = await fixture.ExecuteScriptAsync($"""
            INSERT INTO "FixtureIsolationProbe" ("Value") VALUES ('{marker}');
            """);

        Assert.Equal(0, insertResult.ExitCode);

        var isolatedFixture = new PostgreSqlFixture();
        await isolatedFixture.InitializeAsync();

        try
        {
            var isolationResult = await isolatedFixture.ExecuteScriptAsync($"""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "FixtureIsolationProbe"
                        WHERE "Value" = '{marker}'
                    ) THEN
                        RAISE EXCEPTION 'Dados vazaram entre execuções isoladas.';
                    END IF;
                END $$;
                """);

            Assert.Equal(0, isolationResult.ExitCode);
            Assert.NotEqual(fixture.DatabaseName, isolatedFixture.DatabaseName);
            Assert.NotEqual(fixture.ConnectionString, isolatedFixture.ConnectionString);
        }
        finally
        {
            await isolatedFixture.DisposeAsync();
        }
    }
}
