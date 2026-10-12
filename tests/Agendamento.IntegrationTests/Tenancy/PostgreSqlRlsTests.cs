using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace Agendamento.IntegrationTests.Tenancy;

public sealed class PostgreSqlRlsTests
{
    internal static readonly string[] Tables = ["tenant_fiscal_profiles", "tenant_memberships", "Customers", "ServiceItems",
        "Quotations", "QuotationItem", "WorkOrders", "WorkOrderItems", "Appointments", "ReceivableTitles",
        "ReceivablePayments", "PayableTitles", "PayablePayments", "NotificationMessages", "TenantSubscriptions", "team_invitations"];
    internal sealed class Tenant(Guid id) : ITenantContext { public Guid TenantId => id; public bool HasTenant => id != Guid.Empty; }

    [Fact(DisplayName = "Papel restrito impede SQL direto entre tenants em todas as tabelas @spec:AC-033 @principle:P-004")]
    public async Task DirectSqlCannotCrossTenantBoundaries()
    {
        await using var fixture = await Database.Create();
        // An accidentally broad permissive policy must not weaken the tenant barrier.
        await Execute(fixture.Admin, "CREATE POLICY accidental_allow_all ON \"Customers\" TO agendamento_app_user USING (true) WITH CHECK (true)");
        await using var connection = new NpgsqlConnection(fixture.RestrictedConnection);
        await connection.OpenAsync();
        Assert.True((bool)(await Scalar(connection, "SELECT NOT rolsuper AND NOT rolbypassrls AND NOT EXISTS (SELECT FROM pg_class WHERE relowner=r.oid AND relnamespace='public'::regnamespace) FROM pg_roles r WHERE rolname = current_user"))!);
        await using var tx = await connection.BeginTransactionAsync();
        await Execute(connection, "SELECT set_config('app.tenant_id', @id, true)", fixture.A.ToString());
        foreach (var table in Tables)
        {
            Assert.Equal(1L, await Scalar(connection, $"SELECT count(*) FROM \"{table}\""));
            Assert.Equal(1, await Execute(connection, $"UPDATE \"{table}\" SET tenant_id = tenant_id WHERE tenant_id = @id::uuid", fixture.A.ToString()));
            Assert.Equal(0, await Execute(connection, $"UPDATE \"{table}\" SET tenant_id = tenant_id WHERE tenant_id = @id::uuid", fixture.B.ToString()));
            Assert.Equal(0, await Execute(connection, $"DELETE FROM \"{table}\" WHERE tenant_id = @id::uuid", fixture.B.ToString()));
            await Execute(connection, "SAVEPOINT rejected_write");
            var error = await Assert.ThrowsAsync<PostgresException>(() => Execute(connection,
                $"UPDATE \"{table}\" SET tenant_id = @id::uuid", fixture.B.ToString()));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
            await Execute(connection, "ROLLBACK TO SAVEPOINT rejected_write");
            // A complete valid row is supplied; NOT NULL failures cannot masquerade as RLS proof.
            var row = fixture.Rows[(table, fixture.B)];
            await using var insert = new NpgsqlCommand(row.Sql, connection);
            foreach (var pair in row.Values) insert.Parameters.AddWithValue(pair.Key, pair.Value ?? DBNull.Value);
            error = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
            await Execute(connection, "ROLLBACK TO SAVEPOINT rejected_write");
            Assert.Equal(1L, await Scalar(connection, $"SELECT count(*) FROM \"{table}\""));
        }
        await Execute(connection, "SAVEPOINT foreign_tenant");
        var bCustomer = (Guid)(await Scalar(fixture.Admin, "SELECT \"Id\" FROM \"Customers\" WHERE tenant_id=@id::uuid", fixture.B.ToString()))!;
        var foreignKeyError = await Assert.ThrowsAsync<PostgresException>(() => Execute(connection,
            "UPDATE \"Quotations\" SET \"CustomerId\"=@id::uuid", bCustomer.ToString()));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, foreignKeyError.SqlState);
        await Execute(connection, "ROLLBACK TO SAVEPOINT foreign_tenant");
        await tx.RollbackAsync();
        foreach (var table in Tables)
            Assert.Equal(2L, await Scalar(fixture.Admin, $"SELECT count(*) FROM \"{table}\""));
    }

    [Fact(DisplayName = "Pool reutiliza conexão sem conservar tenant após commit e rollback @spec:AC-034")]
    public async Task PoolAndEfCommandsUseLocalContext()
    {
        await using var fixture = await Database.Create();
        var poolConnection = new NpgsqlConnectionStringBuilder(fixture.RestrictedConnection)
            { MaxPoolSize = 1, MinPoolSize = 1 }.ConnectionString;
        int? pid = null;
        foreach (var id in new[] { fixture.A, fixture.B, fixture.A, fixture.B })
        {
            await using var connection = new NpgsqlConnection(poolConnection);
            await connection.OpenAsync();
            var currentPid = (int)(await Scalar(connection, "SELECT pg_backend_pid()"))!;
            if (pid.HasValue) Assert.Equal(pid, currentPid); else pid = currentPid;
            Assert.True((bool)(await Scalar(connection, "SELECT nullif(current_setting('app.tenant_id', true), '') IS NULL"))!);
            Assert.Equal(0L, await Scalar(connection, "SELECT count(*) FROM \"Customers\""));
            await using (var tx = await connection.BeginTransactionAsync())
            {
                await Execute(connection, "SELECT set_config('app.tenant_id', @id, true)", id.ToString());
                Assert.Equal(id, await Scalar(connection, "SELECT tenant_id FROM \"Customers\""));
                if (id == fixture.A) await tx.CommitAsync(); else await tx.RollbackAsync();
            }
            Assert.Equal(0L, await Scalar(connection, "SELECT count(*) FROM \"Customers\""));
        }
        foreach (var id in new[] { fixture.A, fixture.B })
        {
            var tenant = new Tenant(id);
            var options = new DbContextOptionsBuilder<AgendamentoDbContext>().UseNpgsql(poolConnection)
                .AddInterceptors(new TenantTransactionInterceptor(tenant, new AdministrativeDatabaseScope()), new TenantSaveChangesInterceptor(tenant)).Options;
            await using var db = new AgendamentoDbContext(options, tenant);
            // IgnoreQueryFilters deliberately removes the EF barrier. RLS must still isolate.
            Assert.Equal(id, (await db.Customers.IgnoreQueryFilters().SingleAsync()).TenantId);
            Assert.Equal(id, db.Customers.IgnoreQueryFilters().Single().TenantId);
            await using var transaction = await db.Database.BeginTransactionAsync();
            Assert.Equal(id, (await db.Customers.IgnoreQueryFilters().SingleAsync()).TenantId);
            await transaction.RollbackAsync();
            Assert.Equal(id, (await db.Customers.IgnoreQueryFilters().SingleAsync()).TenantId);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Customers\" SET \"Name\" = 'isolated' WHERE tenant_id = {fixture.B}");
        }
    }

    [Fact(DisplayName = "Migration preserva registros e protege cada tabela do modelo @spec:AC-035")]
    public async Task EveryTenantTableHasRequiredDatabaseProtection()
    {
        await using var fixture = await Database.Create();
        var options = new DbContextOptionsBuilder<AgendamentoDbContext>().UseNpgsql(fixture.MigrationConnection).Options;
        await using var db = new AgendamentoDbContext(options, new Tenant(Guid.Empty));
        var modelTables = db.Model.GetEntityTypes().Where(e => e.FindProperty("TenantId") != null)
            .Select(e => e.GetTableName()!).Distinct().Order().ToArray();
        Assert.Equal(Tables.Order(), modelTables);
        foreach (var table in modelTables)
        {
            Assert.True((bool)(await Scalar(fixture.Admin, """
                SELECT c.relrowsecurity AND c.relforcerowsecurity
                  AND EXISTS (SELECT FROM pg_attribute a WHERE a.attrelid=c.oid AND a.attname='tenant_id' AND a.attnotnull)
                  AND EXISTS (SELECT FROM pg_constraint f JOIN pg_attribute a ON a.attrelid=c.oid AND a.attnum=ANY(f.conkey)
                    WHERE f.conrelid=c.oid AND f.contype='f' AND f.confrelid='tenants'::regclass AND a.attname='tenant_id')
                  AND EXISTS (SELECT FROM pg_index i JOIN pg_attribute a ON a.attrelid=c.oid AND a.attnum=i.indkey[0]
                    WHERE i.indrelid=c.oid AND a.attname='tenant_id')
                  AND EXISTS (SELECT FROM pg_policy p WHERE p.polrelid=c.oid AND p.polname='tenant_isolation'
                    AND p.polqual IS NOT NULL AND p.polwithcheck IS NOT NULL)
                  AND EXISTS (SELECT FROM pg_policy p WHERE p.polrelid=c.oid AND p.polname='tenant_guard' AND NOT p.polpermissive)
                FROM pg_class c WHERE c.oid=quote_ident(@id)::regclass
                """, table))!);
            Assert.Equal(2L, await Scalar(fixture.Admin, $"SELECT count(*) FROM \"{table}\" WHERE tenant_id IS NOT NULL"));
        }
    }

    [Fact(DisplayName = "API rejeita credencial proprietária e bypass antes de consultar dados @spec:AC-033")]
    public async Task RuntimeRejectsMigrationCredential()
    {
        await using var fixture = await Database.Create(seed: false);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:PostgreSql"] = fixture.MigrationConnection
        }).Build();
        var scope = new AdministrativeDatabaseScope();
        var tenant = new Tenant(fixture.A);
        var options = new DbContextOptionsBuilder<AgendamentoDbContext>().UseNpgsql(fixture.MigrationConnection)
            .AddInterceptors(new TenantConnectionInterceptor(config, scope), new TenantTransactionInterceptor(tenant, scope)).Options;
        await using var db = new AgendamentoDbContext(options, tenant);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => db.Customers.ToListAsync());
        Assert.StartsWith("Credencial de execução inválida", error.Message);
    }

    internal static async Task<object?> Scalar(NpgsqlConnection connection, string sql, string? id = null)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        if (id != null) command.Parameters.AddWithValue("id", id);
        return await command.ExecuteScalarAsync();
    }
    internal static async Task<int> Execute(NpgsqlConnection connection, string sql, string? id = null)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        if (id != null) command.Parameters.AddWithValue("id", id);
        return await command.ExecuteNonQueryAsync();
    }

    internal sealed class Database : IAsyncDisposable
    {
        private readonly string _name = "agendamento_rls_" + Guid.NewGuid().ToString("N");
        private readonly string _systemLogin = "rls_system_" + Guid.NewGuid().ToString("N");
        private readonly string _login = "rls_test_" + Guid.NewGuid().ToString("N");
        private NpgsqlConnection _maintenance = null!;
        public NpgsqlConnection Admin { get; private set; } = null!;
        public string MigrationConnection { get; private set; } = null!;
        public string RestrictedConnection { get; private set; } = null!;
        public string AdministrativeConnection { get; private set; } = null!;
        public Guid A { get; } = Guid.NewGuid();
        public Guid B { get; } = Guid.NewGuid();
        public Dictionary<(string, Guid), (string Sql, Dictionary<string, object?> Values)> Rows { get; } = new();
        private bool _created, _roleCreated, _systemRoleCreated;

        public static async Task<Database> Create(bool seed = true, string? migrationTarget = null)
        {
            var fixture = new Database();
            try { await fixture.Initialize(seed, migrationTarget); return fixture; }
            catch { await fixture.DisposeAsync(); throw; }
        }

        private async Task Initialize(bool seed, string? migrationTarget)
        {
            var config = new ConfigurationBuilder().AddUserSecrets<Program>(optional: true).AddEnvironmentVariables().Build();
            var builder = new NpgsqlConnectionStringBuilder(config.GetConnectionString("PostgreSqlMigration") ?? config.GetConnectionString("PostgreSql")
                ?? throw new InvalidOperationException("Configure a conexão PostgreSQL local.")) { Database = "postgres", IncludeErrorDetail = false };
            _maintenance = new NpgsqlConnection(builder.ConnectionString);
            await _maintenance.OpenAsync();
            await Execute(_maintenance, $"CREATE DATABASE \"{_name}\""); _created = true;
            builder.Database = _name;
            MigrationConnection = builder.ConnectionString;
            Admin = new NpgsqlConnection(MigrationConnection); await Admin.OpenAsync();
            var options = new DbContextOptionsBuilder<AgendamentoDbContext>().UseNpgsql(builder.ConnectionString).Options;
            await using (var db = new AgendamentoDbContext(options, new Tenant(Guid.Empty)))
            {
                await db.GetService<IMigrator>().MigrateAsync("20261012002349_AddActiveTeamMembership");
                if (seed) { await Seed(A, "a"); await Seed(B, "b"); }
                await db.GetService<IMigrator>().MigrateAsync(migrationTarget);
                if (migrationTarget == null) Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            }
            foreach (var table in seed ? Tables : [])
            foreach (var id in new[] { A, B })
            {
                await using var query = new NpgsqlCommand($"SELECT * FROM \"{table}\" WHERE tenant_id=@id", Admin);
                query.Parameters.AddWithValue("id", id);
                await using var reader = await query.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                var values = Enumerable.Range(0, reader.FieldCount).ToDictionary(i => "v"+i, i => reader.IsDBNull(i) ? null : reader.GetValue(i));
                var columns = Enumerable.Range(0, reader.FieldCount).Select(i => '"' + reader.GetName(i) + '"');
                Rows[(table, id)] = ($"INSERT INTO \"{table}\" ({string.Join(',', columns)}) VALUES ({string.Join(',', values.Keys.Select(k=>'@'+k))})", values);
            }
            var secret = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            await Execute(_maintenance, $"CREATE ROLE \"{_login}\" LOGIN NOSUPERUSER NOBYPASSRLS NOINHERIT PASSWORD '{secret}'"); _roleCreated = true;
            await Execute(_maintenance, $"GRANT agendamento_app_user TO \"{_login}\"");
            await Execute(_maintenance, $"CREATE ROLE {_systemLogin} LOGIN NOSUPERUSER NOBYPASSRLS NOINHERIT PASSWORD '{secret}'"); _systemRoleCreated = true;
            await Execute(_maintenance, $"GRANT agendamento_system_user TO {_systemLogin}");
            builder.MaxPoolSize = 20; builder.MinPoolSize = 0;
            builder.Username = _systemLogin; builder.Password = secret;
            AdministrativeConnection = builder.ConnectionString;
            builder.Username = _login; builder.Password = secret;
            // The restricted login has no system membership, table ownership, or schema CREATE.
            RestrictedConnection = builder.ConnectionString;
            await using var restricted = new NpgsqlConnection(RestrictedConnection); await restricted.OpenAsync();
            await Execute(restricted, "SET ROLE agendamento_app_user");
            // Npgsql resets session settings on pool return. Make the default role explicit per connection instead.
            builder.Options = "-c role=agendamento_app_user"; RestrictedConnection = builder.ConnectionString;
        }

        private async Task Seed(Guid tenant, string suffix)
        {
            var ids = new Dictionary<string, Guid> { ["tenants"] = tenant };
            foreach (var table in new[] { "tenants", "Users", "SaasPlans" }.Concat(Tables))
            {
                var columns = new List<(string Name, string Type, bool Nullable, string? Parent)>();
                await using (var command = new NpgsqlCommand("""
                    SELECT c.column_name,c.udt_name,c.is_nullable='YES',
                      (SELECT p.relname FROM pg_constraint f JOIN pg_class p ON p.oid=f.confrelid
                       JOIN pg_attribute a ON a.attrelid=f.conrelid AND a.attnum=ANY(f.conkey)
                       WHERE f.conrelid=quote_ident(c.table_name)::regclass AND f.contype='f' AND a.attname=c.column_name LIMIT 1)
                    FROM information_schema.columns c WHERE c.table_schema='public' AND c.table_name=@id ORDER BY c.ordinal_position
                    """, Admin))
                {
                    command.Parameters.AddWithValue("id", table);
                    await using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync()) columns.Add((reader.GetString(0), reader.GetString(1), reader.GetBoolean(2), reader.IsDBNull(3) ? null : reader.GetString(3)));
                }
                if (table != "tenants") ids[table] = Guid.NewGuid();
                await using var insert = new NpgsqlCommand($"INSERT INTO \"{table}\" ({string.Join(',', columns.Select(c=>'"'+c.Name+'"'))}) VALUES ({string.Join(',', columns.Select((_,i)=>"@v"+i))})", Admin);
                for (var i = 0; i < columns.Count; i++)
                {
                    var c = columns[i];
                    object? value = c.Nullable ? null : c.Parent != null ? ids[c.Parent] : c.Name == "Id" ? ids[table] : c.Name == "TenantId" ? tenant : c.Type switch
                    {
                        "uuid" => Guid.NewGuid(), "bool" => true, "int4" => 1, "int8" => 1L,
                        "numeric" => 1m, "timestamptz" => DateTime.UtcNow, "timestamp" => DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
                        "date" => DateOnly.FromDateTime(DateTime.Now), "jsonb" or "json" => "[]", _ => c.Name == "Permissions" ? "[]" : suffix
                    };
                    insert.Parameters.AddWithValue("v"+i, value ?? DBNull.Value);
                }
                await insert.ExecuteNonQueryAsync();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Admin != null) await Admin.DisposeAsync();
            if (_maintenance == null) return;
            // Only resources generated by this fixture are removed. No work database is targeted.
            if (_created) await Execute(_maintenance, $"DROP DATABASE \"{_name}\" WITH (FORCE)");
            if (_systemRoleCreated) await Execute(_maintenance, $"DROP ROLE {_systemLogin}");
            if (_roleCreated) await Execute(_maintenance, $"DROP ROLE \"{_login}\"");
            await _maintenance.DisposeAsync();
        }
    }
}
