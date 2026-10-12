using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Agendamento.Api.Infrastructure.Persistence;

// The administrative credential belongs only to trusted ports; a tenant connection
// cannot SET ROLE to the system role. Migration contexts do not install this interceptor.
public sealed class TenantConnectionInterceptor(IConfiguration configuration, AdministrativeDatabaseScope administrative) : DbConnectionInterceptor
{
    private void SelectConnection(DbConnection connection)
    {
        var key = administrative.IsActive ? "PostgreSqlAdministrative" : "PostgreSql";
        connection.ConnectionString = configuration.GetConnectionString(key)
            ?? throw new InvalidOperationException($"Configure a credencial separada {key} nos segredos locais.");
    }
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    { SelectConnection(connection); return result; }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    { SelectConnection(connection); return ValueTask.FromResult(result); }

    private async Task Validate(DbConnection connection, bool async, CancellationToken ct)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT NOT rolsuper AND NOT rolbypassrls AND NOT rolcreaterole AND NOT rolcreatedb
              AND EXISTS (SELECT FROM pg_roles WHERE rolname=@runtime_role AND NOT rolsuper AND NOT rolbypassrls)
              AND NOT EXISTS (SELECT FROM pg_class WHERE relnamespace='public'::regnamespace
                AND relowner IN (r.oid, (SELECT oid FROM pg_roles WHERE rolname=@runtime_role)) AND relkind IN ('r','p'))
              AND (@system OR NOT pg_has_role(session_user, 'agendamento_system_user', 'MEMBER'))
            FROM pg_roles r WHERE rolname=session_user
            """;
        command.Parameters.Add(new NpgsqlParameter("system", administrative.IsActive));
        command.Parameters.Add(new NpgsqlParameter("runtime_role", administrative.IsActive ? "agendamento_system_user" : "agendamento_app_user"));
        var valid = async ? await command.ExecuteScalarAsync(ct) : command.ExecuteScalar();
        if (valid is not true)
        {
            await connection.CloseAsync();
            throw new InvalidOperationException("Credencial de execução inválida: use papel restrito, sem propriedade de tabelas nem bypass RLS, e separe a porta administrativa.");
        }
    }
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        => Validate(connection, false, default).GetAwaiter().GetResult();
    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
        => Validate(connection, true, cancellationToken);
}
