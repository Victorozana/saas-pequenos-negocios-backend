namespace Agendamento.Infrastructure.Persistence.Configurations;

public static class UserConfiguration
{
    public const string CreateTableSql = """
        CREATE TABLE IF NOT EXISTS "Users" (
            "Id" uuid PRIMARY KEY,
            "Name" varchar(200) NOT NULL,
            "Cpf" varchar(11) NULL,
            "Email" varchar(320) NOT NULL,
            "PasswordHash" text NOT NULL,
            "Status" integer NOT NULL,
            "CreatedAtUtc" timestamptz NOT NULL,
            "EmailVerifiedAtUtc" timestamptz NULL,
            CONSTRAINT "UX_Users_Cpf" UNIQUE ("Cpf"),
            CONSTRAINT "UX_Users_Email" UNIQUE ("Email")
        );
        """;
}
