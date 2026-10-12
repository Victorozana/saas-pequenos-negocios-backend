using System.Data.Common;
using Agendamento.Api.Application.Tenancy;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Agendamento.Api.Infrastructure.Persistence;

// Every EF command, including reads and ExecuteSql, runs with transaction-local context.
// Transactions supplied by handlers remain owned by those handlers.
public sealed class TenantTransactionInterceptor(ITenantContext tenant, AdministrativeDatabaseScope administrative) : DbCommandInterceptor
{
    private readonly Dictionary<DbCommand, DbTransaction> _owned = new();

    private async Task Prepare(DbCommand command, bool async, CancellationToken ct)
    {
        if (command.Connection is not Npgsql.NpgsqlConnection) return;
        if (command.Transaction == null)
        {
            var transaction = async ? await command.Connection.BeginTransactionAsync(ct) : command.Connection.BeginTransaction();
            _owned.Add(command, transaction);
            command.Transaction = transaction;
        }
        try
        {
            using var setup = command.Connection.CreateCommand();
            setup.Transaction = command.Transaction;
            setup.CommandText = administrative.IsActive
                ? "SET LOCAL ROLE agendamento_system_user; SELECT set_config('app.tenant_id', '', true)"
                : "SET LOCAL ROLE agendamento_app_user; SELECT set_config('app.tenant_id', @tenant, true)";
            if (!administrative.IsActive)
            {
                var parameter = setup.CreateParameter();
                parameter.ParameterName = "tenant";
                parameter.Value = tenant.HasTenant ? tenant.TenantId.ToString() : "";
                setup.Parameters.Add(parameter);
            }
            if (async) await setup.ExecuteNonQueryAsync(ct); else setup.ExecuteNonQuery();
        }
        catch
        {
            Finish(command, false);
            throw;
        }
    }

    private void Finish(DbCommand command, bool success)
    {
        if (!_owned.Remove(command, out var transaction)) return;
        try { if (success) transaction.Commit(); else transaction.Rollback(); }
        finally { command.Transaction = null; transaction.Dispose(); }
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r)
    { Prepare(c, false, default).GetAwaiter().GetResult(); return r; }
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r, CancellationToken ct = default)
    { await Prepare(c, true, ct); return r; }
    public override InterceptionResult<int> NonQueryExecuting(DbCommand c, CommandEventData e, InterceptionResult<int> r)
    { Prepare(c, false, default).GetAwaiter().GetResult(); return r; }
    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<int> r, CancellationToken ct = default)
    { await Prepare(c, true, ct); return r; }
    public override InterceptionResult<object> ScalarExecuting(DbCommand c, CommandEventData e, InterceptionResult<object> r)
    { Prepare(c, false, default).GetAwaiter().GetResult(); return r; }
    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<object> r, CancellationToken ct = default)
    { await Prepare(c, true, ct); return r; }
    public override int NonQueryExecuted(DbCommand c, CommandExecutedEventData e, int r) { Finish(c, true); return r; }
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand c, CommandExecutedEventData e, int r, CancellationToken ct = default)
    { Finish(c, true); return ValueTask.FromResult(r); }
    public override object? ScalarExecuted(DbCommand c, CommandExecutedEventData e, object? r) { Finish(c, true); return r; }
    public override ValueTask<object?> ScalarExecutedAsync(DbCommand c, CommandExecutedEventData e, object? r, CancellationToken ct = default)
    { Finish(c, true); return ValueTask.FromResult(r); }
    public override InterceptionResult DataReaderDisposing(DbCommand c, DataReaderDisposingEventData e, InterceptionResult r)
    {
        if (_owned.ContainsKey(c)) { e.DataReader.Close(); Finish(c, true); }
        return r;
    }
    public override void CommandFailed(DbCommand c, CommandErrorEventData e) => Finish(c, false);
    public override Task CommandFailedAsync(DbCommand c, CommandErrorEventData e, CancellationToken ct = default)
    { Finish(c, false); return Task.CompletedTask; }
    public override void CommandCanceled(DbCommand c, CommandEndEventData e) => Finish(c, false);
    public override Task CommandCanceledAsync(DbCommand c, CommandEndEventData e, CancellationToken ct = default)
    { Finish(c, false); return Task.CompletedTask; }
}
