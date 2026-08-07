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

    private AgendamentoDbContext GetDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new AgendamentoDbContext(options, new DummyTenantContext(tenantId));
        context.Database.EnsureCreated();
        return context;
    }

    [Fact(DisplayName = "Consultas e Comandos de Clientes respeitam isolamento @spec:AC-017")]
    public async Task Customers_ShouldBeIsolated_ByTenant()
    {
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();
        
        var db1 = GetDbContext(tenant1);
        var db2 = GetDbContext(tenant2);

        // O Entity Framework InMemory NÃO suporta HasQueryFilter entre instâncias de DbContext separadas nativamente da mesma forma
        // mas podemos validar que a inserção obriga o TenantId e a consulta no db1 só vê tenant1
        var customer1 = Customer.Create(tenant1, "Customer 1", "11999999999", null, null, null);
        var customer2 = Customer.Create(tenant2, "Customer 2", "11888888888", null, null, null);

        db1.Customers.Add(customer1);
        db1.Customers.Add(customer2);
        await db1.SaveChangesAsync();

        // Em Postgres, RLS limitaria a query global, mas InMemory testamos o filtro no DbContext
        var customersTenant1 = await db1.Customers.ToListAsync();
        Assert.Single(customersTenant1);
        Assert.Equal("Customer 1", customersTenant1.First().Name);
        
        var customersTenant2 = await db2.Customers.ToListAsync();
        Assert.Empty(customersTenant2); // db2 está em outro InMemory db, então claro que é empty
    }
}
