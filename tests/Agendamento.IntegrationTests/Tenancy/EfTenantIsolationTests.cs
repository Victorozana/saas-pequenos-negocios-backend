using System.Net;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.Domain.Common;
using Agendamento.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Agendamento.Api.Application.Tenancy;
using System.Reflection;

namespace Agendamento.IntegrationTests.Tenancy;

public class TestTenantOwnedEntity : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class EfTenantIsolationTests : IClassFixture<AgendamentoApiFactory>
{
    private class FakeTenantContext : ITenantContext
    {
        public Guid TenantId { get; set; }
        public bool HasTenant { get; set; }
    }

    private readonly AgendamentoApiFactory _factory;

    public EfTenantIsolationTests(AgendamentoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void DbContext_AppliesGlobalQueryFilter_ForTenantOwnedEntities()
    {
        var tenantContext = new FakeTenantContext { HasTenant = true, TenantId = Guid.NewGuid() };

        var options = new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new AgendamentoDbContext(options, tenantContext);

        var entityType = dbContext.Model.FindEntityType(typeof(Agendamento.Domain.Tenants.TenantMembership));
        Assert.NotNull(entityType);
        
        var queryFilter = entityType.GetQueryFilter();
        Assert.NotNull(queryFilter); 
    }

    [Fact]
    public void SaveChangesInterceptor_PopulatesTenantId_WhenAdded()
    {
        var tenantId = Guid.NewGuid();
        var tenantContext = new FakeTenantContext { HasTenant = true, TenantId = tenantId };

        var interceptor = new TenantSaveChangesInterceptor(tenantContext);

        var options = new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        using var dbContext = new AgendamentoDbContext(options, tenantContext);

        var entry = dbContext.Entry(Agendamento.Domain.Tenants.TenantMembership.Create(Guid.Empty, Guid.NewGuid(), "Role", false));
        entry.State = EntityState.Added;

        dbContext.SaveChanges();

        Assert.Equal(tenantId, entry.Entity.TenantId);
    }
}
