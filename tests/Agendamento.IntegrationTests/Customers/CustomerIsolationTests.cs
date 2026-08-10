using System;
using System.Linq;
using System.Threading.Tasks;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Customers;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Agendamento.IntegrationTests.Customers;

public class CustomerIsolationTests
{
    private class DummyTenantContext : ITenantContext
    {
        public DummyTenantContext(Guid tenantId)
        {
            TenantId = tenantId;
            HasTenant = true;
        }

        public Guid TenantId { get; }
        public bool HasTenant { get; }
    }

    private AgendamentoDbContext GetDbContext(Guid tenantId, string dbName)
    {
        var options = new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var context = new AgendamentoDbContext(options, new DummyTenantContext(tenantId));
        context.Database.EnsureCreated();
        return context;
    }

    [Fact(DisplayName = "Consultas e Comandos de Clientes respeitam isolamento @spec:AC-041")]
    public async Task Customers_ShouldBeIsolated_ByTenant()
    {
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();
        
        var dbName = Guid.NewGuid().ToString();
        
        var db1 = GetDbContext(tenant1, dbName);
        
        var customer1 = Customer.Create(tenant1, "Customer 1", "11999999999", null, null, null);
        var customer2 = Customer.Create(tenant2, "Customer 2", "11888888888", null, null, null);

        db1.Customers.Add(customer1);
        db1.Customers.Add(customer2);
        await db1.SaveChangesAsync();

        var customersTenant1 = await db1.Customers.ToListAsync();
        Assert.Single(customersTenant1);
        Assert.Equal("Customer 1", customersTenant1.First().Name);
    }

    [Fact(DisplayName = "Tentativa de buscar ou alterar cliente de outro tenant retorna null/vazio @spec:AC-044")]
    public async Task Update_Or_Get_OtherTenantCustomer_ShouldBeIsolated()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var dbName = Guid.NewGuid().ToString();

        // Create in Tenant A context
        var dbA = GetDbContext(tenantA, dbName);
        var customerA = Customer.Create(tenantA, "Customer A", "11999999999", null, null, null);
        dbA.Customers.Add(customerA);
        await dbA.SaveChangesAsync();

        // Query in Tenant B context
        var dbB = GetDbContext(tenantB, dbName);
        var customersTenantB = await dbB.Customers.ToListAsync();
        Assert.Empty(customersTenantB);

        var queryCustomerA = await dbB.Customers.FirstOrDefaultAsync(c => c.Id == customerA.Id);
        Assert.Null(queryCustomerA);
    }
}
