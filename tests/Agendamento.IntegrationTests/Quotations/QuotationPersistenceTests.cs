using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.IntegrationTests.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;
using static Agendamento.IntegrationTests.Tenancy.PostgreSqlRlsTests;

namespace Agendamento.IntegrationTests.Quotations;

public sealed class QuotationPersistenceTests
{
    private static AgendamentoDbContext Context(Database fixture, Guid tenantId, bool restricted = true, bool guard = true)
    {
        var tenant = new PostgreSqlRlsTests.Tenant(tenantId);
        var options = new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseNpgsql(restricted ? fixture.RestrictedConnection : fixture.MigrationConnection);
        if (restricted) options.AddInterceptors(new TenantTransactionInterceptor(tenant, new AdministrativeDatabaseScope()));
        if (guard) options.AddInterceptors(new TenantSaveChangesInterceptor(tenant));
        return new AgendamentoDbContext(options.Options, tenant);
    }

    private static async Task<Guid> Id(Database fixture, string table, Guid tenant)
        => (Guid)(await Scalar(fixture.Admin, $"SELECT \"Id\" FROM \"{table}\" WHERE tenant_id=@id::uuid", tenant.ToString()))!;

    [Fact(DisplayName = "PostgreSQL roundtrip completo de orçamento itens sinal e todos estados @spec:AC-092")]
    public async Task CompleteAggregateRoundtrip()
    {
        await using var fixture = await Database.Create();
        var customer = await Id(fixture, "Customers", fixture.A);
        var service = await Id(fixture, "ServiceItems", fixture.A);
        var originals = new List<Quotation>();
        await using (var db = Context(fixture, fixture.A))
        {
            foreach (var status in Enum.GetValues<QuotationStatus>())
            {
                var expired = status == QuotationStatus.Expired;
                var issue = DateTime.UtcNow.Date.AddDays(-10);
                var quote = Quotation.Create(fixture.A, "roundtrip-" + status, customer, issue,
                    expired ? issue.AddDays(1) : DateTime.UtcNow.Date.AddDays(10),
                    "Notas com acentos ç " + new string('n', 1980), new string('p', 2000));
                quote.AddItem(QuotationItem.Create(service, "Nome histórico ç", "hora", 10.01m, 1.25m, .51m));
                quote.AddItem(QuotationItem.Create(null, "Item avulso", "un", 0m, .01m, 0m));
                quote.SetDeposit(status == QuotationStatus.Draft ? DepositType.FixedAmount : DepositType.Percentage,
                    status == QuotationStatus.Draft ? 1.23m : 33.33m, new string('s', 1000));
                if (status != QuotationStatus.Draft) quote.MarkAsPending();
                switch (status)
                {
                    case QuotationStatus.Approved: quote.Approve(); break;
                    case QuotationStatus.Rejected: quote.Reject(); break;
                    case QuotationStatus.Expired: quote.CheckExpiration(); break;
                    case QuotationStatus.Canceled: quote.Cancel(); break;
                    case QuotationStatus.Converted: quote.Approve(); quote.MarkAsConverted(); break;
                }
                db.Add(quote);
                if (status == QuotationStatus.Approved)
                    db.Entry(quote).Reference(q => q.DepositInfo).TargetEntry!.Property(nameof(DepositInfo.IsPaid)).CurrentValue = true;
                originals.Add(quote);
            }
            var optional = Quotation.Create(fixture.A, "roundtrip-null", customer, DateTime.UtcNow.Date, null, null, null);
            optional.AddItem(QuotationItem.Create(null, "Sem sinal", "un", 9999999999999999.99m, 1m, 0m));
            originals.Add(optional);
            db.Add(optional);
            var zero = Quotation.Create(fixture.A, "roundtrip-zero", customer, DateTime.UtcNow.Date, null, null, null);
            zero.AddItem(QuotationItem.Create(null, "Gratuito", "un", 0m, 1m, 0m));
            zero.SetDeposit(DepositType.Percentage, 0m, null);
            originals.Add(zero);
            db.Add(zero);
            await db.SaveChangesAsync();
        }
        await using (var db = Context(fixture, fixture.A))
        {
            var loaded = await db.Quotations.Include(q => q.Items).Include(q => q.Customer)
                .Where(q => q.Code.StartsWith("roundtrip-")).ToListAsync();
            Assert.Equal(originals.Count, loaded.Count);
            foreach (var expected in originals)
            {
                var actual = Assert.Single(loaded, q => q.Id == expected.Id);
                Assert.Equal(expected.TenantId, actual.TenantId);
                Assert.Equal(expected.Code, actual.Code);
                Assert.Equal(expected.CustomerId, actual.CustomerId);
                Assert.Equal(customer, actual.Customer!.Id);
                Assert.Equal(expected.IssueDate, actual.IssueDate);
                Assert.Equal(expected.ValidUntil, actual.ValidUntil);
                Assert.Equal(expected.Status, actual.Status);
                Assert.Equal(expected.Notes, actual.Notes);
                Assert.Equal(expected.PaymentTerms, actual.PaymentTerms);
                Assert.Equal(expected.SubtotalAmount, actual.SubtotalAmount);
                Assert.Equal(expected.DiscountAmount, actual.DiscountAmount);
                Assert.Equal(expected.TotalAmount, actual.TotalAmount);
                Assert.Equal(expected.DepositInfo, actual.DepositInfo);
                Assert.Equal(expected.Items.Count, actual.Items.Count);
                foreach (var item in expected.Items)
                {
                    var persisted = Assert.Single(actual.Items, i => i.Id == item.Id);
                    Assert.Equal(actual.Id, persisted.QuotationId);
                    Assert.Equal(item.ServiceItemId, persisted.ServiceItemId);
                    Assert.Equal(item.ServiceName, persisted.ServiceName);
                    Assert.Equal(item.Unit, persisted.Unit);
                    Assert.Equal(item.UnitPrice, persisted.UnitPrice);
                    Assert.Equal(item.Quantity, persisted.Quantity);
                    Assert.Equal(item.DiscountAmount, persisted.DiscountAmount);
                    Assert.Equal(item.TotalPrice, persisted.TotalPrice);
                    Assert.Equal(fixture.A, db.Entry(persisted).Property<Guid>("TenantId").CurrentValue);
                }
            }
            // Exercise materialized backing collection and owned optionality through an actual UPDATE.
            var draft = loaded.Single(q => q.Code == "roundtrip-Draft");
            var removed = draft.Items.Single(i => i.ServiceItemId == service).Id;
            draft.RemoveDeposit();
            draft.RemoveItem(removed);
            var replacement = QuotationItem.Create(null, "Reposição", "un", 2.22m, 2m, .44m);
            draft.AddItem(replacement);
            db.Add(replacement);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var refreshed = await db.Quotations.Include(q => q.Items).SingleAsync(q => q.Id == draft.Id);
            Assert.Null(refreshed.DepositInfo);
            Assert.DoesNotContain(refreshed.Items, i => i.Id == removed);
            Assert.Contains(refreshed.Items, i => i.ServiceName == "Reposição" && i.TotalPrice == 4m);
            Assert.Equal(4m, refreshed.TotalAmount);
        }
    }

    [Fact(DisplayName = "Filtros EF e RLS isolam orçamento e item inclusive IgnoreQueryFilters @spec:AC-092")]
    public async Task AggregateAndStandaloneDependentsAreIsolated()
    {
        await using var fixture = await Database.Create();
        foreach (var tenant in new[] { fixture.A, fixture.B, Guid.Empty })
        {
            await using (var owner = Context(fixture, tenant, restricted: false))
            {
                Assert.Equal(tenant == Guid.Empty ? 0 : 1, await owner.Quotations.CountAsync());
                Assert.Equal(tenant == Guid.Empty ? 0 : 1, await owner.Set<QuotationItem>().CountAsync());
                Assert.Equal(2, await owner.Set<QuotationItem>().IgnoreQueryFilters().CountAsync());
            }
            await using var restricted = Context(fixture, tenant);
            Assert.Equal(tenant == Guid.Empty ? 0 : 1, await restricted.Quotations.IgnoreQueryFilters().CountAsync());
            var items = await restricted.Set<QuotationItem>().IgnoreQueryFilters().ToListAsync();
            Assert.Equal(tenant == Guid.Empty ? 0 : 1, items.Count);
            foreach (var item in items) Assert.Equal(tenant, restricted.Entry(item).Property<Guid>("TenantId").CurrentValue);
        }
        await using var connection = new NpgsqlConnection(fixture.RestrictedConnection);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await Execute(connection, "SELECT set_config('app.tenant_id', @id, true)", fixture.A.ToString());
        var foreignBefore = await Scalar(fixture.Admin,
            "SELECT row_to_json(q)::text FROM \"Quotations\" q WHERE tenant_id=@id::uuid", fixture.B.ToString());
        Assert.Equal(0, await Execute(connection,
            "UPDATE \"Quotations\" SET \"DepositIsPaid\"=true WHERE tenant_id=@id::uuid", fixture.B.ToString()));
        Assert.Equal(foreignBefore, await Scalar(fixture.Admin,
            "SELECT row_to_json(q)::text FROM \"Quotations\" q WHERE tenant_id=@id::uuid", fixture.B.ToString()));
        foreach (var table in new[] { "Quotations", "QuotationItem" })
        {
            Assert.Equal(1L, await Scalar(connection, $"SELECT count(*) FROM \"{table}\""));
            Assert.Equal(0, await Execute(connection, $"UPDATE \"{table}\" SET tenant_id=tenant_id WHERE tenant_id=@id::uuid", fixture.B.ToString()));
            Assert.Equal(0, await Execute(connection, $"DELETE FROM \"{table}\" WHERE tenant_id=@id::uuid", fixture.B.ToString()));
            await Execute(connection, "SAVEPOINT blocked");
            var error = await Assert.ThrowsAsync<PostgresException>(() => Execute(connection,
                $"UPDATE \"{table}\" SET tenant_id=@id::uuid", fixture.B.ToString()));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
            await Execute(connection, "ROLLBACK TO SAVEPOINT blocked");
        }
        await transaction.RollbackAsync();
        Assert.Equal(2L, await Scalar(fixture.Admin, "SELECT count(*) FROM \"QuotationItem\""));
    }

    [Fact(DisplayName = "Integridade física impede referências cruzadas e inválidas sem mutar linhas @spec:AC-092")]
    public async Task ReferentialIntegrityAndDeleteRules()
    {
        await using var fixture = await Database.Create();
        var foreignCustomer = await Id(fixture, "Customers", fixture.B);
        var foreignQuote = await Id(fixture, "Quotations", fixture.B);
        var foreignService = await Id(fixture, "ServiceItems", fixture.B);
        var localService = await Id(fixture, "ServiceItems", fixture.A);
        var localQuote = await Id(fixture, "Quotations", fixture.A);
        await using var connection = new NpgsqlConnection(fixture.RestrictedConnection);
        await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();
        await Execute(connection, "SELECT set_config('app.tenant_id', @id, true)", fixture.A.ToString());
        var beforeQuote = (await Scalar(connection, "SELECT row_to_json(q)::text FROM \"Quotations\" q"))!;
        var beforeItem = (await Scalar(connection, "SELECT row_to_json(q)::text FROM \"QuotationItem\" q"))!;
        Assert.Equal(1, await Execute(connection, "UPDATE \"QuotationItem\" SET \"ServiceItemId\"=@id::uuid", localService.ToString()));
        beforeItem = (await Scalar(connection, "SELECT row_to_json(q)::text FROM \"QuotationItem\" q"))!;
        foreach (var (sql, id, state) in new[]
        {
            ("UPDATE \"Quotations\" SET \"CustomerId\"=@id::uuid", foreignCustomer, PostgresErrorCodes.ForeignKeyViolation),
            ("UPDATE \"Quotations\" SET \"CustomerId\"=@id::uuid", Guid.NewGuid(), PostgresErrorCodes.ForeignKeyViolation),
            ("UPDATE \"QuotationItem\" SET \"QuotationId\"=@id::uuid", foreignQuote, PostgresErrorCodes.ForeignKeyViolation),
            ("UPDATE \"QuotationItem\" SET \"QuotationId\"=@id::uuid", Guid.NewGuid(), PostgresErrorCodes.ForeignKeyViolation),
            ("UPDATE \"QuotationItem\" SET \"ServiceItemId\"=@id::uuid", foreignService, PostgresErrorCodes.ForeignKeyViolation),
            ("UPDATE \"QuotationItem\" SET \"ServiceItemId\"=@id::uuid", Guid.NewGuid(), PostgresErrorCodes.ForeignKeyViolation),
            ("DELETE FROM \"ServiceItems\" WHERE \"Id\"=@id::uuid", localService, PostgresErrorCodes.RestrictViolation),
            ("UPDATE \"Quotations\" SET \"CustomerId\"=NULL", Guid.Empty, PostgresErrorCodes.NotNullViolation),
            ("UPDATE \"Quotations\" SET tenant_id=NULL", Guid.Empty, PostgresErrorCodes.InsufficientPrivilege),
            ("DELETE FROM \"Customers\" WHERE tenant_id=@id::uuid", fixture.A, PostgresErrorCodes.RestrictViolation),
            ("DELETE FROM tenants WHERE \"Id\"=@id::uuid", fixture.A, PostgresErrorCodes.InsufficientPrivilege)
        })
        {
            await Execute(connection, "SAVEPOINT rejected");
            var error = await Assert.ThrowsAsync<PostgresException>(() => Execute(connection, sql, id.ToString()));
            Assert.Equal(state, error.SqlState);
            await Execute(connection, "ROLLBACK TO SAVEPOINT rejected");
            Assert.Equal(beforeQuote, await Scalar(connection, "SELECT row_to_json(q)::text FROM \"Quotations\" q"));
            Assert.Equal(beforeItem, await Scalar(connection, "SELECT row_to_json(q)::text FROM \"QuotationItem\" q"));
        }
        Assert.Equal(1, await Execute(connection, "UPDATE \"QuotationItem\" SET \"ServiceItemId\"=NULL"));
        await tx.RollbackAsync();
        // Under the migration owner, RLS cannot hide a missing NOT NULL constraint.
        await using (var ownerTx = await fixture.Admin.BeginTransactionAsync())
        {
            foreach (var table in new[] { "Quotations", "QuotationItem" })
            {
                await Execute(fixture.Admin, "SAVEPOINT required_tenant");
                var error = await Assert.ThrowsAsync<PostgresException>(() => Execute(fixture.Admin,
                    $"UPDATE \"{table}\" SET tenant_id=NULL WHERE tenant_id=@id::uuid", fixture.A.ToString()));
                Assert.Equal(PostgresErrorCodes.NotNullViolation, error.SqlState);
                await Execute(fixture.Admin, "ROLLBACK TO SAVEPOINT required_tenant");
            }
            await ownerTx.RollbackAsync();
        }
        // Delete a fresh aggregate so unrelated work order/financial FKs cannot mask the item cascade.
        await using var db = Context(fixture, fixture.A);
        var quote = Quotation.Create(fixture.A, "cascade", await Id(fixture, "Customers", fixture.A), DateTime.UtcNow.Date, null, null, null);
        var dependent = QuotationItem.Create(null, "Cascade", "un", 1m, 1m, 0m);
        quote.AddItem(dependent);
        db.Add(quote);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"Quotations\" WHERE \"Id\"={quote.Id}");
        Assert.False(await db.Set<QuotationItem>().AnyAsync(i => i.Id == dependent.Id));
        Assert.True(await db.Quotations.AnyAsync(q => q.Id == localQuote));
    }

    [Fact(DisplayName = "Código único por tenant e gravação rejeitada preservam agregado @spec:AC-092")]
    public async Task UniqueCodeAndWriteGuards()
    {
        await using var fixture = await Database.Create();
        foreach (var tenant in new[] { fixture.A, fixture.B })
        {
            await using var db = Context(fixture, tenant);
            var quote = Quotation.Create(tenant, "same-code", await Id(fixture, "Customers", tenant), DateTime.UtcNow.Date, null, null, null);
            quote.AddItem(QuotationItem.Create(null, "Unique", "un", 1m, 1m, 0m));
            db.Add(quote);
            await db.SaveChangesAsync();
        }
        await using (var db = Context(fixture, fixture.A))
        {
            var duplicate = Quotation.Create(fixture.A, "same-code", await Id(fixture, "Customers", fixture.A), DateTime.UtcNow.Date, null, null, null);
            duplicate.AddItem(QuotationItem.Create(null, "Duplicado", "un", 1m, 1m, 0m));
            db.Add(duplicate);
            var failure = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(failure.InnerException).SqlState);
            db.ChangeTracker.Clear();
            Assert.Equal(1, await db.Quotations.CountAsync(q => q.Code == "same-code"));
            Assert.False(await db.Set<QuotationItem>().AnyAsync(i => i.Id == duplicate.Items.Single().Id));
        }
        await using (var db = Context(fixture, fixture.A, restricted: false))
        {
            var foreign = await db.Quotations.IgnoreQueryFilters().Include(q => q.Items).SingleAsync(q => q.TenantId == fixture.B && q.Code == "same-code");
            foreign.Cancel();
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            var unchanged = await db.Quotations.IgnoreQueryFilters().SingleAsync(q => q.Id == foreign.Id);
            Assert.Equal(QuotationStatus.Draft, unchanged.Status);
            var item = await db.Set<QuotationItem>().IgnoreQueryFilters().SingleAsync(i => i.QuotationId == foreign.Id);
            db.Entry(item).Property(i => i.ServiceName).CurrentValue = "Forbidden";
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            Assert.Equal("Unique", (await db.Set<QuotationItem>().IgnoreQueryFilters().SingleAsync(i => i.Id == item.Id)).ServiceName);
        }
    }

    [Fact(DisplayName = "Migration de referência catálogo preserva itens históricos válidos e sinal @spec:AC-092")]
    public async Task CatalogMigrationPreservesValidHistoricalData()
    {
        await using var fixture = await Database.Create(migrationTarget: "20261012191900_AddDailyReceivableReminderDeduplication");
        await using (var db = Context(fixture, fixture.A))
        {
            var quote = Quotation.Create(fixture.A, "upgrade", await Id(fixture, "Customers", fixture.A), DateTime.UtcNow.Date, null,
                "Histórico preservado", "Parcelado");
            quote.AddItem(QuotationItem.Create(await Id(fixture, "ServiceItems", fixture.A), "Catálogo histórico", "un", 12.34m, 1.25m, .43m));
            quote.AddItem(QuotationItem.Create(null, "Manual histórico", "h", 0m, 1m, 0m));
            quote.SetDeposit(DepositType.Percentage, 25m, "Sinal histórico");
            quote.MarkAsPending();
            db.Add(quote);
            await db.SaveChangesAsync();
        }
        var before = await AggregateRows(fixture);
        await using (var migrator = Context(fixture, Guid.Empty, restricted: false, guard: false))
        {
            await migrator.GetService<IMigrator>().MigrateAsync();
            Assert.Empty(await migrator.Database.GetPendingMigrationsAsync());
        }
        Assert.Equal(before, await AggregateRows(fixture));
        await using var after = Context(fixture, fixture.A);
        var loaded = await after.Quotations.Include(q => q.Items).SingleAsync(q => q.Code == "upgrade");
        Assert.Equal(QuotationStatus.Pending, loaded.Status);
        Assert.Equal(2, loaded.Items.Count);
        Assert.Contains(loaded.Items, i => i.ServiceItemId == null);
        Assert.Equal(3.75m, loaded.DepositInfo!.RequiredAmount);
        Assert.Equal(11.25m, loaded.DepositInfo.RemainingBalance);
    }

    [Theory(DisplayName = "Migration recusa catálogo histórico externo ou órfão preservando dados e histórico @spec:AC-092")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CatalogMigrationRejectsInvalidHistoricalData(bool orphan)
    {
        await using var fixture = await Database.Create(migrationTarget: "20261012191900_AddDailyReceivableReminderDeduplication");
        var invalid = orphan ? Guid.NewGuid() : await Id(fixture, "ServiceItems", fixture.B);
        await Execute(fixture.Admin, "UPDATE \"QuotationItem\" SET \"ServiceItemId\"=@id::uuid WHERE tenant_id='" + fixture.A + "'::uuid", invalid.ToString());
        var before = await AggregateRows(fixture);
        var history = await Scalar(fixture.Admin, "SELECT json_agg(h ORDER BY \"MigrationId\")::text FROM \"__EFMigrationsHistory\" h");
        await using var migrator = Context(fixture, Guid.Empty, restricted: false, guard: false);
        var error = await Assert.ThrowsAsync<PostgresException>(() => migrator.GetService<IMigrator>().MigrateAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        Assert.Equal(before, await AggregateRows(fixture));
        Assert.Equal(history, await Scalar(fixture.Admin, "SELECT json_agg(h ORDER BY \"MigrationId\")::text FROM \"__EFMigrationsHistory\" h"));
        Assert.Single(await migrator.Database.GetPendingMigrationsAsync());
        Assert.Null(await Scalar(fixture.Admin,
            "SELECT conname FROM pg_constraint WHERE conname='FK_QuotationItem_ServiceItems_tenant_id_ServiceItemId'"));
    }

    private static async Task<string[]> AggregateRows(Database fixture)
        => [(string)(await Scalar(fixture.Admin, "SELECT json_agg(q ORDER BY \"Id\")::text FROM \"Quotations\" q"))!,
            (string)(await Scalar(fixture.Admin, "SELECT json_agg(i ORDER BY \"Id\")::text FROM \"QuotationItem\" i"))!];
}
