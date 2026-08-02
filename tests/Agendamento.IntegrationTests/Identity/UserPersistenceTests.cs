using Agendamento.Domain.Identity;
using Agendamento.Infrastructure.Persistence.Configurations;
using Agendamento.Infrastructure.Identity;
using Agendamento.IntegrationTests.Infrastructure;
using Xunit;

namespace Agendamento.IntegrationTests.Identity;

[Collection(PostgreSqlCollection.Name)]
public sealed class UserPersistenceTests(PostgreSqlFixture fixture)
{
    [Fact(DisplayName = "CPF e e-mail normalizados têm unicidade global na persistência @spec:AC-007")]
    public async Task PersistingUsers_WithDuplicateNormalizedCpfOrEmail_ConflictsWithoutCreatingAnotherIdentity()
    {
        await fixture.ExecuteScriptAsync(UserConfiguration.CreateTableSql);

        var passwords = new AspNetPasswordService();
        var registeredUser = User.Create("123.456.789-09", "admin@example.com", passwords.Hash("uma-senha-segura"));
        var sameCpf = User.Create("12345678909", "other@example.com", passwords.Hash("uma-senha-segura"));
        var sameEmail = User.Create("987.654.321-00", " ADMIN@EXAMPLE.COM ", passwords.Hash("uma-senha-segura"));

        var result = await fixture.ExecuteScriptAsync($"""
            INSERT INTO "Users" ("Id", "Cpf", "Email", "PasswordHash", "Status", "CreatedAtUtc")
            VALUES ({Values(registeredUser)});

            DO $$
            BEGIN
                BEGIN
                    INSERT INTO "Users" ("Id", "Cpf", "Email", "PasswordHash", "Status", "CreatedAtUtc")
                    VALUES ({Values(sameCpf)});
                    RAISE EXCEPTION 'CPF duplicado foi aceito.';
                EXCEPTION WHEN unique_violation THEN
                    NULL;
                END;

                BEGIN
                    INSERT INTO "Users" ("Id", "Cpf", "Email", "PasswordHash", "Status", "CreatedAtUtc")
                    VALUES ({Values(sameEmail)});
                    RAISE EXCEPTION 'E-mail duplicado foi aceito.';
                EXCEPTION WHEN unique_violation THEN
                    NULL;
                END;

                IF (SELECT COUNT(*) FROM "Users") <> 1 THEN
                    RAISE EXCEPTION 'Uma identidade duplicada foi criada.';
                END IF;
            END $$;
            """);

        Assert.Equal(0, result.ExitCode);
    }

    private static string Values(User user) => $"""
        '{user.Id}',
        '{user.Cpf}',
        '{user.Email}',
        '{user.PasswordHash.Replace("'", "''", StringComparison.Ordinal)}',
        {(int)user.Status},
        '{user.CreatedAtUtc:O}'
        """;
}
