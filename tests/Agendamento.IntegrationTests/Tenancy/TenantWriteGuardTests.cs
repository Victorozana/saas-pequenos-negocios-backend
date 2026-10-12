using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Customers;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Agendamento.IntegrationTests.Tenancy;

public sealed class TenantWriteGuardTests
{
    private sealed class TenantContext(Guid id) : ITenantContext
    {
        public Guid TenantId => id;
        public bool HasTenant => true;
    }

    private static AgendamentoDbContext Context(string database, Guid tenant, bool guard = true)
    {
        var context = new TenantContext(tenant);
        var options = new DbContextOptionsBuilder<AgendamentoDbContext>().UseInMemoryDatabase(database);
        if (guard) options.AddInterceptors(new TenantSaveChangesInterceptor(context));
        return new AgendamentoDbContext(options.Options, context);
    }

    [Fact(DisplayName = "Consultas retornam somente registros da empresa autenticada @spec:AC-030")]
    public async Task ReadsOnlyAuthenticatedTenant()
    {
        var database = Guid.NewGuid().ToString();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        using (var seed = Context(database, tenantA, guard: false))
        {
            seed.Customers.AddRange(Customer.Create(tenantA, "A", "1", null, null, null),
                Customer.Create(tenantB, "B", "2", null, null, null));
            await seed.SaveChangesAsync();
        }
        using var a = Context(database, tenantA);
        using var b = Context(database, tenantB);
        Assert.Equal("A", Assert.Single(await a.Customers.ToListAsync()).Name);
        Assert.Equal("B", Assert.Single(await b.Customers.ToListAsync()).Name);
    }

    [Theory(DisplayName = "Gravações divergentes falham antes do commit @spec:AC-032")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectsForeignInsertWithoutPersistingAnyChanges(bool asynchronous)
    {
        var database = Guid.NewGuid().ToString();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        using var writer = Context(database, tenantA);
        writer.Customers.Add(Customer.Create(tenantA, "Valid", "1", null, null, null));
        writer.Customers.Add(Customer.Create(tenantB, "Invalid", "2", null, null, null));
        if (asynchronous) await Assert.ThrowsAsync<InvalidOperationException>(() => writer.SaveChangesAsync());
        else Assert.Throws<InvalidOperationException>(() => writer.SaveChanges());
        using var reader = Context(database, tenantA);
        Assert.Empty(await reader.Customers.IgnoreQueryFilters().ToListAsync());
    }

    [Theory(DisplayName = "Alteração e exclusão de registro de outra empresa são rejeitadas @spec:AC-031 @spec:AC-032")]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RejectsForeignUpdateOrDelete(bool delete, bool rewriteTenant)
    {
        var database = Guid.NewGuid().ToString();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var customer = Customer.Create(tenantB, "Original", "1", null, null, null);
        using (var seed = Context(database, tenantB))
        {
            seed.Customers.Add(customer);
            await seed.SaveChangesAsync();
        }
        using (var attacker = Context(database, tenantA))
        {
            var foreign = await attacker.Customers.IgnoreQueryFilters().SingleAsync();
            if (rewriteTenant) foreign.TenantId = tenantA;
            if (delete) attacker.Customers.Remove(foreign);
            else foreign.Update("Changed", "2", null, null, null);
            await Assert.ThrowsAsync<InvalidOperationException>(() => attacker.SaveChangesAsync());
        }
        using var reader = Context(database, tenantB);
        var unchanged = Assert.Single(await reader.Customers.ToListAsync());
        Assert.Equal(customer.Id, unchanged.Id);
        Assert.Equal(tenantB, unchanged.TenantId);
        Assert.Equal("Original", unchanged.Name);
    }
}
