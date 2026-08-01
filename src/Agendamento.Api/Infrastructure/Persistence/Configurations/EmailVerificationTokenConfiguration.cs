namespace Agendamento.Infrastructure.Persistence.Configurations;

/// <summary>
/// Persistence schema contract for email verification tokens. The EF Core adapter is
/// intentionally registered by composition root when persistence is introduced.
/// </summary>
public static class EmailVerificationTokenConfiguration
{
    public const string TableName = "email_verification_tokens";
    public const int DigestLength = 64;

    public const string CreateTableSql = """
        CREATE TABLE IF NOT EXISTS "EmailVerificationTokens" (
            "Id" uuid PRIMARY KEY,
            "UserId" uuid NOT NULL,
            "Digest" varchar(64) NOT NULL UNIQUE,
            "ExpiresAtUtc" timestamptz NOT NULL,
            "CreatedAtUtc" timestamptz NOT NULL,
            "ConsumedAtUtc" timestamptz NULL,
            CONSTRAINT "FK_EmailVerificationTokens_Users_UserId"
                FOREIGN KEY ("UserId") REFERENCES "Users" ("Id")
        );

        CREATE INDEX IF NOT EXISTS "IX_EmailVerificationTokens_UserId"
            ON "EmailVerificationTokens" ("UserId");
        """;
}
