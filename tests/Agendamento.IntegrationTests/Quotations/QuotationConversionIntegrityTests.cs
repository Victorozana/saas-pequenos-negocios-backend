using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Domain.WorkOrders;
using Agendamento.Api.Domain.Services;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.IntegrationTests.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;
using static Agendamento.IntegrationTests.Tenancy.PostgreSqlRlsTests;

namespace Agendamento.IntegrationTests.Quotations;

public sealed class QuotationConversionIntegrityTests
{
    private const string Previous = "20261013173339_EnforceQuotationCatalogTenantReference";
    private const string Current = "20261013202849_EnforceWorkOrderConversionIntegrity";

    private static AgendamentoDbContext Context(Database fixture, Guid id, bool restricted = true)
    {
        var tenant = new PostgreSqlRlsTests.Tenant(id);
        var options = new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseNpgsql(restricted ? fixture.RestrictedConnection : fixture.MigrationConnection);
        if (restricted) options.AddInterceptors(new TenantTransactionInterceptor(tenant, new AdministrativeDatabaseScope()),
            new TenantSaveChangesInterceptor(tenant));
        return new(options.Options, tenant);
    }

    private static async Task<Guid> Id(Database fixture, string table, Guid tenant)
        => (Guid)(await Scalar(fixture.Admin, $"SELECT \"Id\" FROM \"{table}\" WHERE tenant_id=@id::uuid", tenant.ToString()))!;

    private static async Task<string[]> Rows(Database fixture)
        => [(string)(await Scalar(fixture.Admin, "SELECT json_agg(w ORDER BY \"Id\")::text FROM \"WorkOrders\" w"))!,
            (string)(await Scalar(fixture.Admin, "SELECT json_agg(i ORDER BY \"Id\")::text FROM \"WorkOrderItems\" i"))!];

    [Fact(DisplayName = "Conversão modelo e snapshot preservam textos, precisão, unique e catálogo composto nullable Restrict @spec:AC-091 @spec:AC-092")]
    public void MappingMatchesSnapshot()
    {
        using var db = new AgendamentoDbContext(new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only").Options, new PostgreSqlRlsTests.Tenant(Guid.NewGuid()));
        var model = db.GetService<IDesignTimeModel>().Model;
        var order = model.FindEntityType(typeof(WorkOrder))!;
        var item = order.FindNavigation(nameof(WorkOrder.Items))!.TargetEntityType;
        Assert.Equal(53, order.FindProperty(nameof(WorkOrder.Code))!.GetMaxLength());
        Assert.Equal(2000, order.FindProperty(nameof(WorkOrder.Notes))!.GetMaxLength());
        Assert.Null(item.FindProperty(nameof(WorkOrderItem.ServiceName))!.GetMaxLength());
        Assert.Null(item.FindProperty(nameof(WorkOrderItem.Unit))!.GetMaxLength());
        Assert.Equal("text", item.FindProperty(nameof(WorkOrderItem.ServiceName))!.GetColumnType());
        Assert.Contains(order.GetIndexes(), i => i.IsUnique && i.Properties.Select(p => p.Name)
            .SequenceEqual(new[] { "TenantId", "QuotationId" }));
        var catalog = Assert.Single(item.GetForeignKeys(), f => f.PrincipalEntityType.ClrType == typeof(ServiceItem));
        Assert.Equal(new[] { "TenantId", "ServiceItemId" }, catalog.Properties.Select(p => p.Name));
        Assert.Equal(new[] { "TenantId", "Id" }, catalog.PrincipalKey.Properties.Select(p => p.Name));
        Assert.True(item.FindProperty(nameof(WorkOrderItem.ServiceItemId))!.IsNullable);
        Assert.Equal(DeleteBehavior.Restrict, catalog.DeleteBehavior);
        foreach (var name in new[] { "Quantity", "UnitPrice", "DiscountAmount" })
        {
            Assert.Equal(18, item.FindProperty(name)!.GetPrecision());
            Assert.Equal(2, item.FindProperty(name)!.GetScale());
        }
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(Current, db.Database.GetMigrations().Last());
    }

    [Fact(DisplayName = "PostgreSQL unique OS e catálogo nullable/local/externo/inexistente Restrict protegidos por SQL físico @spec:AC-091 @spec:AC-092")]
    public async Task PhysicalConstraintsRejectInvalidWrites()
    {
        await using var fixture = await Database.Create();
        var order = await Id(fixture, "WorkOrders", fixture.A);
        var local = await Id(fixture, "ServiceItems", fixture.A);
        var foreign = await Id(fixture, "ServiceItems", fixture.B);
        await using var db = Context(fixture, fixture.A);
        var duplicate = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"WorkOrders\" (\"Id\",tenant_id,\"Code\",\"CustomerId\",\"QuotationId\",\"CreatedAt\",\"Status\") SELECT {Guid.NewGuid()},tenant_id,\"Code\",\"CustomerId\",\"QuotationId\",\"CreatedAt\",\"Status\" FROM \"WorkOrders\" WHERE \"Id\"={order}"));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
        Assert.Equal("IX_WorkOrders_tenant_id_QuotationId", duplicate.ConstraintName);
        Assert.Equal(1, await db.WorkOrders.CountAsync());
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"WorkOrderItems\" SET \"ServiceItemId\"={local} WHERE \"WorkOrderId\"={order}");
        foreach (var invalid in new[] { foreign, Guid.NewGuid() })
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"WorkOrderItems\" SET \"ServiceItemId\"={invalid} WHERE \"WorkOrderId\"={order}"));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
            Assert.Equal("FK_WorkOrderItems_ServiceItems_tenant_id_ServiceItemId", error.ConstraintName);
            Assert.Equal(local, (await db.WorkOrders.AsNoTracking().Include(w => w.Items).SingleAsync()).Items.Single().ServiceItemId);
        }
        // Remove quotation reference so only the new work-order FK can reject deletion.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"QuotationItem\" SET \"ServiceItemId\"=NULL WHERE tenant_id={fixture.A}");
        var delete = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"ServiceItems\" WHERE \"Id\"={local}"));
        Assert.Equal(PostgresErrorCodes.RestrictViolation, delete.SqlState);
        Assert.Equal("FK_WorkOrderItems_ServiceItems_tenant_id_ServiceItemId", delete.ConstraintName);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"WorkOrderItems\" SET \"ServiceItemId\"=NULL WHERE \"WorkOrderId\"={order}");
        Assert.Null((await db.WorkOrders.AsNoTracking().Include(w => w.Items).SingleAsync()).Items.Single().ServiceItemId);
        await using var foreignDb = Context(fixture, fixture.B);
        Assert.Empty(await foreignDb.WorkOrders.IgnoreQueryFilters().Where(w => w.Id == order).Include(w => w.Items).ToListAsync());
    }

    [Fact(DisplayName = "Migration conversão preserva linhas históricas válidas byte a byte @spec:AC-091 @spec:AC-092")]
    public async Task UpgradePreservesValidHistory()
    {
        await using var fixture = await Database.Create(migrationTarget: Previous);
        var local = await Id(fixture, "ServiceItems", fixture.A);
        await Execute(fixture.Admin, "UPDATE \"WorkOrderItems\" SET \"ServiceItemId\"=@id::uuid WHERE tenant_id='" + fixture.A + "'::uuid", local.ToString());
        var before = await Rows(fixture);
        await using var migrator = Context(fixture, Guid.Empty, restricted: false);
        await migrator.GetService<IMigrator>().MigrateAsync();
        Assert.Empty(await migrator.Database.GetPendingMigrationsAsync());
        Assert.Equal(before, await Rows(fixture));
        Assert.NotNull(await Scalar(fixture.Admin, "SELECT conname FROM pg_constraint WHERE conname='FK_WorkOrderItems_ServiceItems_tenant_id_ServiceItemId'"));
    }

    [Theory(DisplayName = "Migration conversão recusa histórico inválido sem limpeza e rollback de índices/textos/histórico @spec:AC-091 @spec:AC-092")]
    [InlineData("external")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    public async Task UpgradeRejectsInvalidHistoryAtomically(string kind)
    {
        await using var fixture = await Database.Create(migrationTarget: Previous);
        if (kind == "duplicate")
            await Execute(fixture.Admin, "INSERT INTO \"WorkOrders\" (\"Id\",tenant_id,\"Code\",\"CustomerId\",\"QuotationId\",\"CreatedAt\",\"Status\") SELECT gen_random_uuid(),tenant_id,\"Code\",\"CustomerId\",\"QuotationId\",\"CreatedAt\",\"Status\" FROM \"WorkOrders\" WHERE tenant_id='" + fixture.A + "'::uuid");
        else
            await Execute(fixture.Admin, "UPDATE \"WorkOrderItems\" SET \"ServiceItemId\"=@id::uuid WHERE tenant_id='" + fixture.A + "'::uuid", (kind == "external" ? await Id(fixture, "ServiceItems", fixture.B) : Guid.NewGuid()).ToString());
        var before = await Rows(fixture);
        var history = await Scalar(fixture.Admin, "SELECT json_agg(h ORDER BY \"MigrationId\")::text FROM \"__EFMigrationsHistory\" h");
        await using var migrator = Context(fixture, Guid.Empty, restricted: false);
        var error = await Assert.ThrowsAsync<PostgresException>(() => migrator.GetService<IMigrator>().MigrateAsync());
        Assert.Equal(kind == "duplicate" ? PostgresErrorCodes.UniqueViolation : PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        Assert.Equal(before, await Rows(fixture));
        Assert.Equal(history, await Scalar(fixture.Admin, "SELECT json_agg(h ORDER BY \"MigrationId\")::text FROM \"__EFMigrationsHistory\" h"));
        Assert.Equal(Current, Assert.Single(await migrator.Database.GetPendingMigrationsAsync()));
        Assert.Null(await Scalar(fixture.Admin, "SELECT conname FROM pg_constraint WHERE conname='FK_WorkOrderItems_ServiceItems_tenant_id_ServiceItemId'"));
        Assert.Null(await Scalar(fixture.Admin, "SELECT indexname FROM pg_indexes WHERE indexname='IX_WorkOrders_tenant_id_QuotationId'"));
        Assert.Equal(50, Convert.ToInt32(await Scalar(fixture.Admin, "SELECT character_maximum_length FROM information_schema.columns WHERE table_name='WorkOrders' AND column_name='Code'")));
        Assert.Equal(1000, Convert.ToInt32(await Scalar(fixture.Admin, "SELECT character_maximum_length FROM information_schema.columns WHERE table_name='WorkOrders' AND column_name='Notes'")));
    }

    [Theory(DisplayName = "Guarda EF síncrona de item OS recusa shadow original/current e owner adulterados @spec:AC-092")]
    [InlineData("item-current")]
    [InlineData("item-original")]
    [InlineData("owner-current")]
    [InlineData("owner-original")]
    public void SynchronousOwnedItemGuardRejectsTenantTampering(string operation)
    {
        var tenantId = Guid.NewGuid();
        var other = Guid.NewGuid();
        var name = Guid.NewGuid().ToString();
        AgendamentoDbContext Memory()
        {
            var tenant = new PostgreSqlRlsTests.Tenant(tenantId);
            return new(new DbContextOptionsBuilder<AgendamentoDbContext>().UseInMemoryDatabase(name)
                .AddInterceptors(new TenantSaveChangesInterceptor(tenant)).Options, tenant);
        }
        Guid orderId;
        using (var seed = Memory())
        {
            var quote = Quotation.Create(tenantId, "sync-owned", Guid.NewGuid(), DateTime.UtcNow, null, null, null);
            quote.AddItem(QuotationItem.Create(null, "Original", "un", 1m, 1m, 0m));
            quote.MarkAsPending(); quote.Approve();
            var order = WorkOrder.CreateFromQuotation(quote);
            orderId = order.Id;
            seed.Add(order); seed.SaveChanges();
        }
        using (var db = Memory())
        {
            var order = db.WorkOrders.Include(w => w.Items).Single();
            var item = order.Items.Single();
            db.Entry(item).Property(i => i.ServiceName).CurrentValue = "Forbidden";
            var itemTenant = db.Entry(item).Property<Guid>("TenantId");
            var ownerTenant = db.Entry(order).Property(w => w.TenantId);
            switch (operation)
            {
                case "item-current": itemTenant.CurrentValue = other; break;
                case "item-original": itemTenant.OriginalValue = other; break;
                case "owner-current": ownerTenant.CurrentValue = other; break;
                case "owner-original": ownerTenant.OriginalValue = other; break;
            }
            Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
            Assert.Equal("Forbidden", item.ServiceName);
        }
        using var verify = Memory();
        var persisted = verify.WorkOrders.Include(w => w.Items).Single();
        Assert.Equal(orderId, persisted.Id);
        Assert.Equal(tenantId, persisted.TenantId);
        Assert.Equal(tenantId, verify.Entry(persisted.Items.Single()).Property<Guid>("TenantId").CurrentValue);
        Assert.Equal("Original", persisted.Items.Single().ServiceName);
    }

    [Theory(DisplayName = "Guarda EF owned OS recusa item externo ou sem contexto com owner inalterado @spec:AC-092")]
    [InlineData("modified")]
    [InlineData("deleted")]
    [InlineData("added")]
    [InlineData("absent")]
    public async Task OwnedItemGuardPreservesDatabase(string operation)
    {
        var tenantId = Guid.NewGuid();
        var other = Guid.NewGuid();
        var name = Guid.NewGuid().ToString();
        AgendamentoDbContext Memory(Guid id)
        {
            var tenant = new PostgreSqlRlsTests.Tenant(id);
            return new(new DbContextOptionsBuilder<AgendamentoDbContext>().UseInMemoryDatabase(name)
                .AddInterceptors(new TenantSaveChangesInterceptor(tenant)).Options, tenant);
        }
        using (var seed = Memory(tenantId))
        {
            var quote = Quotation.Create(tenantId, "owned", Guid.NewGuid(), DateTime.UtcNow, null, null, null);
            quote.AddItem(QuotationItem.Create(null, "Original", "un", 1m, 1m, 0m));
            quote.MarkAsPending(); quote.Approve();
            seed.Add(WorkOrder.CreateFromQuotation(quote));
            await seed.SaveChangesAsync();
        }
        using (var db = Memory(operation == "absent" ? Guid.Empty : operation == "added" ? tenantId : other))
        {
            var order = await db.WorkOrders.IgnoreQueryFilters().Include(w => w.Items).SingleAsync();
            var item = order.Items.Single();
            if (operation == "deleted") db.Entry(item).State = EntityState.Deleted;
            else db.Entry(item).Property(i => i.ServiceName).CurrentValue = "Forbidden";
            if (operation == "added")
            {
                db.Entry(item).State = EntityState.Added;
                db.Entry(item).Property<Guid>("TenantId").CurrentValue = other;
            }
            Assert.Equal(EntityState.Unchanged, db.Entry(order).State);
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
        using var verify = Memory(tenantId);
        Assert.Equal("Original", (await verify.WorkOrders.Include(w => w.Items).SingleAsync()).Items.Single().ServiceName);
    }
}
