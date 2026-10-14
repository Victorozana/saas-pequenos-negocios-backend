using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Services;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Agendamento.IntegrationTests.Services;

public sealed class ServiceCatalogMappingTests
{
    private sealed class TenantContext(Guid id) : ITenantContext
    {
        public Guid TenantId => id;
        public bool HasTenant => id != Guid.Empty;
    }

    private static AgendamentoDbContext Context(string name, Guid id)
    {
        var tenant = new TenantContext(id);
        return new(new DbContextOptionsBuilder<AgendamentoDbContext>().UseInMemoryDatabase(name)
            .AddInterceptors(new TenantSaveChangesInterceptor(tenant)).Options, tenant);
    }

    [Fact(DisplayName = "Catálogo metadata efetiva e cadeia descoberta preservam snapshot sem migration pendente @spec:AC-045")]
    public void EffectiveMetadataAndDiscoveredMigrations()
    {
        using var db = new AgendamentoDbContext(new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata_only").Options, new TenantContext(Guid.NewGuid()));
        var entity = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ServiceItem))!;
        Assert.Equal("ServiceItems", entity.GetTableName());
        Assert.Equal("Id", Assert.Single(entity.FindPrimaryKey()!.Properties).Name);
        Assert.Equal(typeof(Guid), entity.FindProperty("Id")!.ClrType);
        Assert.Equal(ValueGenerated.OnAdd, entity.FindProperty("Id")!.ValueGenerated);
        foreach (var name in new[] { "Id", "TenantId", "Name", "Unit", "BasePrice", "IsActive" })
            Assert.False(entity.FindProperty(name)!.IsNullable);
        Assert.True(entity.FindProperty("Description")!.IsNullable);
        Assert.Equal("tenant_id", entity.FindProperty("TenantId")!.GetColumnName());
        Assert.Equal(200, entity.FindProperty("Name")!.GetMaxLength());
        Assert.Equal(1000, entity.FindProperty("Description")!.GetMaxLength());
        Assert.Equal(50, entity.FindProperty("Unit")!.GetMaxLength());
        Assert.Equal("character varying(200)", entity.FindProperty("Name")!.GetColumnType());
        Assert.Equal("character varying(1000)", entity.FindProperty("Description")!.GetColumnType());
        Assert.Equal("character varying(50)", entity.FindProperty("Unit")!.GetColumnType());
        var price = entity.FindProperty("BasePrice")!;
        Assert.Equal("numeric(18,2)", price.GetColumnType());
        Assert.Equal(18, price.GetPrecision());
        Assert.Equal(2, price.GetScale());
        Assert.Equal("boolean", entity.FindProperty("IsActive")!.GetColumnType());
        Assert.NotNull(entity.GetQueryFilter());
        Assert.Contains(entity.GetKeys(), k => k.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "Id" }));
        Assert.Contains(entity.GetIndexes(), i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId" }));
        Assert.Contains(entity.GetIndexes(), i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "IsActive", "Name" }));
        var tenantFk = Assert.Single(entity.GetForeignKeys(), f => f.PrincipalEntityType.ClrType == typeof(Tenant));
        Assert.True(tenantFk.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, tenantFk.DeleteBehavior);
        var migrations = db.Database.GetMigrations().ToArray();
        Assert.Equal(new[] {
            "20260807002238_InitialTenant", "20260807225823_AddCustomerTable",
            "20260807231536_AddQuotationsAndServices", "20260808001400_AddWorkOrdersAndAppointments",
            "20260808005011_AddFinancialTables", "20260809234023_AddNotificationModule",
            "20260810001025_AddBillingAndSubscriptions", "20260810231913_UpdateNotificationsBackoff",
            "20260811235746_AddEmailVerificationTokens", "20261012002349_AddActiveTeamMembership",
            "20261012041401_EnforceTenantRowLevelSecurity", "20261012154514_AddNotificationCustomerHistory",
            "20261012174238_AddCustomerAutomaticReminderPreference", "20261012191900_AddDailyReceivableReminderDeduplication",
            "20261013173339_EnforceQuotationCatalogTenantReference", "20261013202849_EnforceWorkOrderConversionIntegrity"
        }, migrations);
        Assert.Equal(migrations.OrderBy(m => m, StringComparer.Ordinal), migrations);
        Assert.DoesNotContain(migrations, m => m.EndsWith("_AddRowLevelSecurity"));
        Assert.Contains("20260807231536_AddQuotationsAndServices", migrations);
        Assert.EndsWith("_EnforceWorkOrderConversionIntegrity", migrations.Last());
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact(DisplayName = "Filtros EF de catálogo isolam tenants e contexto ausente @spec:AC-045")]
    public async Task FiltersRequireMatchingTenant()
    {
        var name = Guid.NewGuid().ToString();
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        foreach (var id in ids)
        {
            using var seed = Context(name, id);
            seed.ServiceItems.Add(ServiceItem.Create(id, "Serviço", null, "m²", 0m));
            await seed.SaveChangesAsync();
        }
        foreach (var id in ids)
        {
            using var reader = Context(name, id);
            Assert.Equal(id, Assert.Single(await reader.ServiceItems.ToListAsync()).TenantId);
            Assert.Equal(2, await reader.ServiceItems.IgnoreQueryFilters().CountAsync());
        }
        using var absent = Context(name, Guid.Empty);
        Assert.Empty(await absent.ServiceItems.ToListAsync());
    }

    [Theory(DisplayName = "Guarda catálogo verifica original current e contexto em lote sem gravação parcial @spec:AC-045")]
    [InlineData("added", false)] [InlineData("added", true)]
    [InlineData("foreign-update", false)] [InlineData("foreign-update", true)]
    [InlineData("foreign-delete", false)] [InlineData("foreign-delete", true)]
    [InlineData("original-rewrite", false)] [InlineData("original-rewrite", true)]
    [InlineData("current-update", false)] [InlineData("current-update", true)]
    [InlineData("current-delete", false)] [InlineData("current-delete", true)]
    [InlineData("absent-added", false)] [InlineData("absent-added", true)]
    [InlineData("absent-update", false)] [InlineData("absent-update", true)]
    [InlineData("absent-delete", false)] [InlineData("absent-delete", true)]
    public async Task InvalidBatchCannotPersist(string operation, bool asynchronous)
    {
        var name = Guid.NewGuid().ToString();
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var originalTenant = operation.StartsWith("foreign") || operation == "original-rewrite" ? b : a;
        Guid id;
        using (var seed = Context(name, originalTenant))
        {
            var service = ServiceItem.Create(originalTenant, "Original", "Descrição", "hora", 1.23m);
            id = service.Id; seed.Add(service); await seed.SaveChangesAsync();
        }
        using (var writer = Context(name, operation.StartsWith("absent") ? Guid.Empty : a))
        {
            if (operation.EndsWith("added")) writer.Add(ServiceItem.Create(b, "Inválido", null, "un", 0m));
            else
            {
                var service = await writer.ServiceItems.IgnoreQueryFilters().SingleAsync();
                if (operation == "original-rewrite") service.TenantId = a;
                if (operation.StartsWith("current")) service.TenantId = b;
                if (operation.EndsWith("delete")) writer.Remove(service);
                else service.Update("Alterado", null, "un", 2m);
            }
            writer.Add(ServiceItem.Create(a, "Válido enfileirado", null, "un", 0m));
            if (asynchronous) await Assert.ThrowsAsync<InvalidOperationException>(() => writer.SaveChangesAsync());
            else Assert.Throws<InvalidOperationException>(() => writer.SaveChanges());
        }
        using var reader = Context(name, originalTenant);
        var unchanged = Assert.Single(await reader.ServiceItems.IgnoreQueryFilters().ToListAsync());
        Assert.Equal(id, unchanged.Id); Assert.Equal(originalTenant, unchanged.TenantId);
        Assert.Equal("Original", unchanged.Name); Assert.Equal("Descrição", unchanged.Description);
        Assert.Equal("hora", unchanged.Unit); Assert.Equal(1.23m, unchanged.BasePrice); Assert.True(unchanged.IsActive);
    }
}
