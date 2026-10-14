using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Agendamento.IntegrationTests.Quotations;

public sealed class QuotationMappingTests
{
    private sealed class TenantContext(Guid id) : ITenantContext
    {
        public Guid TenantId => id;
        public bool HasTenant => id != Guid.Empty;
    }

    private static AgendamentoDbContext Context(string database, Guid tenant)
    {
        var context = new TenantContext(tenant);
        return new(new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseInMemoryDatabase(database).AddInterceptors(new TenantSaveChangesInterceptor(context)).Options, context);
    }

    private static Quotation Create(Guid tenant, string code)
    {
        var quotation = Quotation.Create(tenant, code, Guid.NewGuid(), DateTime.UtcNow, null, "Notes", "Terms");
        quotation.AddItem(QuotationItem.Create(null, "Service", "hour", 12.34m, 2m, 0.68m));
        quotation.SetDeposit(DepositType.Percentage, 25m, "Deposit notes");
        quotation.MarkAsPending();
        return quotation;
    }

    private static async Task Seed(string database, params Guid[] tenants)
    {
        foreach (var tenant in tenants)
        {
            using var seed = Context(database, tenant);
            seed.Quotations.Add(Create(tenant, tenant.ToString()));
            await seed.SaveChangesAsync();
        }
    }

    [Fact(DisplayName = "Mapeamento efetivo de orçamento mantém chaves, precisão, owned, coleção e snapshot @spec:AC-092")]
    public void EffectiveMappingAndSnapshotRemainConsistent()
    {
        using var db = new AgendamentoDbContext(new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseNpgsql("Host=localhost;Database=mapping_only").Options, new TenantContext(Guid.NewGuid()));
        var model = db.GetService<IDesignTimeModel>().Model;
        var quotation = model.FindEntityType(typeof(Quotation))!;
        var item = model.FindEntityType(typeof(QuotationItem))!;
        Assert.Equal("Quotations", quotation.GetTableName());
        Assert.Equal("QuotationItem", item.GetTableName());
        Assert.Equal("Id", Assert.Single(quotation.FindPrimaryKey()!.Properties).Name);
        Assert.Equal("Id", Assert.Single(item.FindPrimaryKey()!.Properties).Name);
        foreach (var property in new[] { "TenantId", "CustomerId", "Code", "IssueDate", "Status", "SubtotalAmount", "DiscountAmount", "TotalAmount" })
            Assert.False(quotation.FindProperty(property)!.IsNullable);
        Assert.Equal("tenant_id", quotation.FindProperty("TenantId")!.GetColumnName());
        Assert.Equal("tenant_id", item.FindProperty("TenantId")!.GetColumnName());
        Assert.False(item.FindProperty("TenantId")!.IsNullable);
        Assert.NotNull(quotation.GetQueryFilter());
        Assert.NotNull(item.GetQueryFilter());
        Assert.Equal(typeof(QuotationStatus), quotation.FindProperty("Status")!.ClrType);
        Assert.Equal("integer", quotation.FindProperty("Status")!.GetColumnType());
        foreach (var name in new[] { "SubtotalAmount", "DiscountAmount", "TotalAmount" })
        {
            Assert.Equal(18, quotation.FindProperty(name)!.GetPrecision());
            Assert.Equal(2, quotation.FindProperty(name)!.GetScale());
        }
        // Item numeric columns are deliberately unbounded at the database layer;
        // the domain validates the two-decimal input contract before persistence.
        foreach (var name in new[] { "UnitPrice", "Quantity", "DiscountAmount", "TotalPrice" })
            Assert.Equal("numeric", item.FindProperty(name)!.GetColumnType());
        Assert.True(item.FindProperty("ServiceItemId")!.IsNullable);
        Assert.False(item.FindProperty("ServiceName")!.IsNullable);
        Assert.False(item.FindProperty("Unit")!.IsNullable);
        var items = quotation.FindNavigation("Items")!;
        Assert.Equal("_items", items.FieldInfo!.Name);
        Assert.Equal(PropertyAccessMode.Field, items.GetPropertyAccessMode());
        Assert.Equal(DeleteBehavior.Cascade, items.ForeignKey.DeleteBehavior);
        Assert.Equal("QuotationId", Assert.Single(items.ForeignKey.Properties).Name);
        Assert.Equal(DeleteBehavior.Restrict, quotation.FindNavigation("Customer")!.ForeignKey.DeleteBehavior);
        Assert.Contains(quotation.GetIndexes(), i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "Code" }));
        Assert.Contains(quotation.GetIndexes(), i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "Status" }));
        Assert.Contains(quotation.GetIndexes(), i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "CustomerId" }));
        var deposit = quotation.FindNavigation("DepositInfo")!.TargetEntityType;
        Assert.False(quotation.FindNavigation("DepositInfo")!.ForeignKey.IsRequiredDependent);
        Assert.True(deposit.IsOwned());
        Assert.Equal("Quotations", deposit.GetTableName());
        Assert.Equal("DepositIsPaid", deposit.FindProperty("IsPaid")!.GetColumnName());
        Assert.Equal(50, quotation.FindProperty("Code")!.GetMaxLength());
        Assert.Equal(2000, quotation.FindProperty("Notes")!.GetMaxLength());
        Assert.Equal(2000, quotation.FindProperty("PaymentTerms")!.GetMaxLength());
        Assert.Equal(1000, deposit.FindProperty("PaymentNotes")!.GetMaxLength());
        Assert.True(deposit.FindProperty("PaymentNotes")!.IsNullable);
        var quotationTable = model.GetRelationalModel().Tables.Single(t => t.Name == "Quotations");
        foreach (var name in new[] { "DepositType", "DepositValue", "DepositRequiredAmount", "DepositRemainingBalance", "DepositPaymentNotes", "DepositIsPaid" })
            Assert.True(quotationTable.Columns.Single(c => c.Name == name).IsNullable);
        var catalogForeignKey = Assert.Single(item.GetForeignKeys(), f => f.PrincipalEntityType.ClrType == typeof(Agendamento.Api.Domain.Services.ServiceItem));
        Assert.Equal(new[] { "TenantId", "ServiceItemId" }, catalogForeignKey.Properties.Select(p => p.Name));
        Assert.Equal(new[] { "TenantId", "Id" }, catalogForeignKey.PrincipalKey.Properties.Select(p => p.Name));
        Assert.Equal(DeleteBehavior.Restrict, catalogForeignKey.DeleteBehavior);
        Assert.False(catalogForeignKey.IsRequired);
        Assert.Contains(catalogForeignKey.PrincipalEntityType.GetKeys(), k => k.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "Id" }));
        foreach (var name in new[] { "Value", "RequiredAmount", "RemainingBalance" })
        {
            Assert.Equal(18, deposit.FindProperty(name)!.GetPrecision());
            Assert.Equal(2, deposit.FindProperty(name)!.GetScale());
        }
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.DoesNotContain(db.Database.GetMigrations(), m => m.EndsWith("_AddRowLevelSecurity"));
        Assert.Contains(db.Database.GetMigrations(), m => m.EndsWith("_EnforceTenantRowLevelSecurity"));
        Assert.Contains(db.Database.GetMigrations(), m => m.EndsWith("_EnforceQuotationCatalogTenantReference"));
        Assert.EndsWith("_EnforceWorkOrderConversionIntegrity", db.Database.GetMigrations().Last());
    }

    [Fact(DisplayName = "Filtros EF isolam orçamento e consulta direta de itens inclusive sem tenant @spec:AC-092")]
    public async Task FiltersApplyToAggregateAndDirectItemQueries()
    {
        var database = Guid.NewGuid().ToString();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        await Seed(database, a, b);
        foreach (var tenant in new[] { a, b })
        {
            using var db = Context(database, tenant);
            var quotation = Assert.Single(await db.Quotations.Include(q => q.Items).ToListAsync());
            Assert.Equal(tenant, quotation.TenantId);
            var item = Assert.Single(await db.Set<QuotationItem>().ToListAsync());
            Assert.Equal(quotation.Id, item.QuotationId);
            Assert.Equal(tenant, db.Entry(item).Property<Guid>("TenantId").CurrentValue);
        }
        using var noTenant = Context(database, Guid.Empty);
        Assert.Empty(await noTenant.Quotations.ToListAsync());
        Assert.Empty(await noTenant.Set<QuotationItem>().ToListAsync());
    }

    [Theory(DisplayName = "Guarda de itens bloqueia atualização/exclusão foreign e adulteração antes de qualquer gravação @spec:AC-092")]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task ForeignItemWritesRejectWithoutPersisting(bool asynchronous, bool delete, bool rewriteTenant)
    {
        var database = Guid.NewGuid().ToString();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        await Seed(database, b);
        using (var attacker = Context(database, a))
        {
            var item = await attacker.Set<QuotationItem>().IgnoreQueryFilters().SingleAsync();
            if (rewriteTenant) attacker.Entry(item).Property<Guid>("TenantId").CurrentValue = a;
            if (delete) attacker.Remove(item);
            else attacker.Entry(item).Property("ServiceName").CurrentValue = "Changed";
            attacker.Quotations.Add(Create(a, "Valid must not persist"));
            if (asynchronous) await Assert.ThrowsAsync<InvalidOperationException>(() => attacker.SaveChangesAsync());
            else Assert.Throws<InvalidOperationException>(() => attacker.SaveChanges());
        }
        using var reader = Context(database, b);
        Assert.Single(await reader.Quotations.IgnoreQueryFilters().ToListAsync());
        var unchanged = Assert.Single(await reader.Set<QuotationItem>().IgnoreQueryFilters().ToListAsync());
        Assert.Equal("Service", unchanged.ServiceName);
        Assert.Equal(b, reader.Entry(unchanged).Property<Guid>("TenantId").CurrentValue);
    }

    [Theory(DisplayName = "Guarda bloqueia item novo com tenant externo ou contexto ausente sem gravar @spec:AC-092")]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task NewItemRequiresAuthenticatedMatchingTenant(bool asynchronous, bool noTenant)
    {
        var database = Guid.NewGuid().ToString();
        var tenant = Guid.NewGuid();
        using (var writer = Context(database, noTenant ? Guid.Empty : tenant))
        {
            var item = QuotationItem.Create(null, "New", "unit", 1m, 1m, 0m);
            writer.Add(item);
            if (!noTenant) writer.Entry(item).Property<Guid>("TenantId").CurrentValue = Guid.NewGuid();
            if (asynchronous) await Assert.ThrowsAsync<InvalidOperationException>(() => writer.SaveChangesAsync());
            else Assert.Throws<InvalidOperationException>(() => writer.SaveChanges());
            Assert.Equal(EntityState.Added, writer.Entry(item).State);
        }
        using var reader = Context(database, tenant);
        Assert.Empty(await reader.Set<QuotationItem>().IgnoreQueryFilters().ToListAsync());
    }

    [Theory(DisplayName = "Guarda owned recusa sinal rastreado sem seu orçamento antes de gravar @spec:AC-092")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnedWithoutTrackedOwnerCannotWrite(bool asynchronous)
    {
        var database = Guid.NewGuid().ToString();
        var tenant = Guid.NewGuid();
        await Seed(database, tenant);
        using (var writer = Context(database, tenant))
        {
            var quotation = await writer.Quotations.SingleAsync();
            var deposit = quotation.DepositInfo!;
            writer.Entry(quotation).State = EntityState.Detached;
            var entry = writer.Entry(deposit);
            entry.Property("QuotationId").CurrentValue = quotation.Id;
            entry.State = EntityState.Modified;
            entry.Property("IsPaid").CurrentValue = true;
            Assert.Empty(writer.ChangeTracker.Entries<Quotation>());
            if (asynchronous) await Assert.ThrowsAsync<InvalidOperationException>(() => writer.SaveChangesAsync());
            else Assert.Throws<InvalidOperationException>(() => writer.SaveChanges());
        }
        using var reader = Context(database, tenant);
        Assert.False((await reader.Quotations.SingleAsync()).DepositInfo!.IsPaid);
    }

    [Theory(DisplayName = "Guarda owned bloqueia mutação isolada de sinal foreign mesmo com owner inalterado @spec:AC-092")]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task ForeignOwnedWritesRejectWithoutPersisting(bool asynchronous, bool delete, bool rewriteTenant)
    {
        var database = Guid.NewGuid().ToString();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        await Seed(database, b);
        using (var attacker = Context(database, a))
        {
            var quotation = await attacker.Quotations.IgnoreQueryFilters().SingleAsync();
            var deposit = quotation.DepositInfo!;
            Assert.Equal(EntityState.Unchanged, attacker.Entry(quotation).State);
            if (rewriteTenant)
            {
                quotation.TenantId = a;
                attacker.Entry(quotation).State = EntityState.Unchanged;
            }
            if (delete) attacker.Entry(deposit).State = EntityState.Deleted;
            else attacker.Entry(deposit).Property("IsPaid").CurrentValue = true;
            Assert.Equal(EntityState.Unchanged, attacker.Entry(quotation).State);
            attacker.Quotations.Add(Create(a, "Must not persist"));
            if (asynchronous) await Assert.ThrowsAsync<InvalidOperationException>(() => attacker.SaveChangesAsync());
            else Assert.Throws<InvalidOperationException>(() => attacker.SaveChanges());
        }
        using var reader = Context(database, b);
        var original = Assert.Single(await reader.Quotations.IgnoreQueryFilters().ToListAsync());
        Assert.Equal(b, original.TenantId);
        Assert.NotNull(original.DepositInfo);
        Assert.False(original.DepositInfo.IsPaid);
        Assert.Equal(6m, original.DepositInfo.RequiredAmount);
    }

    [Theory(DisplayName = "Guarda owned aceita atualização e exclusão próprias do sinal @spec:AC-092")]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task OwnOwnedWritesPersist(bool asynchronous, bool delete)
    {
        var database = Guid.NewGuid().ToString();
        var tenant = Guid.NewGuid();
        await Seed(database, tenant);
        using (var writer = Context(database, tenant))
        {
            var quotation = await writer.Quotations.SingleAsync();
            if (delete) writer.Entry(quotation.DepositInfo!).State = EntityState.Deleted;
            else writer.Entry(quotation.DepositInfo!).Property("IsPaid").CurrentValue = true;
            Assert.Equal(EntityState.Unchanged, writer.Entry(quotation).State);
            if (asynchronous) await writer.SaveChangesAsync();
            else writer.SaveChanges();
        }
        using var reader = Context(database, tenant);
        var saved = await reader.Quotations.SingleAsync();
        if (delete) Assert.Null(saved.DepositInfo);
        else Assert.True(saved.DepositInfo!.IsPaid);
    }

    [Theory(DisplayName = "Guarda aceita alteração/exclusão de item próprio e rejeita tenant atual adulterado @spec:AC-092")]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task OwnItemWritesAndTampering(bool asynchronous, bool delete)
    {
        var database = Guid.NewGuid().ToString();
        var tenant = Guid.NewGuid();
        await Seed(database, tenant);
        using (var writer = Context(database, tenant))
        {
            var item = await writer.Set<QuotationItem>().SingleAsync();
            writer.Entry(item).Property<Guid>("TenantId").CurrentValue = Guid.NewGuid();
            if (delete) writer.Remove(item);
            else writer.Entry(item).Property("ServiceName").CurrentValue = "Changed";
            if (asynchronous) await Assert.ThrowsAsync<InvalidOperationException>(() => writer.SaveChangesAsync());
            else Assert.Throws<InvalidOperationException>(() => writer.SaveChanges());
        }
        using (var valid = Context(database, tenant))
        {
            var item = await valid.Set<QuotationItem>().SingleAsync();
            Assert.Equal("Service", item.ServiceName);
            if (delete) valid.Remove(item);
            else valid.Entry(item).Property("ServiceName").CurrentValue = "Valid change";
            if (asynchronous) await valid.SaveChangesAsync();
            else valid.SaveChanges();
        }
        using var reader = Context(database, tenant);
        if (delete) Assert.Empty(await reader.Set<QuotationItem>().ToListAsync());
        else Assert.Equal("Valid change", (await reader.Set<QuotationItem>().SingleAsync()).ServiceName);
    }
}
