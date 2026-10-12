using System;
using System.Linq;
using System.Threading.Tasks;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Customers;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Microsoft.Extensions.DependencyInjection;

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

    [Fact(DisplayName = "Lista e detalhe HTTP mascaram CPF, preservam CNPJ e permitem editar sem reenviar documento @spec:AC-038 @principle:P-005")]
    public async Task Customer_http_outputs_minimize_personal_document()
    {
        using var factory = new Agendamento.IntegrationTests.Infrastructure.AgendamentoApiFactory();
        using var client = factory.CreateClient();
        var tenant = Guid.NewGuid();
        var userId = await factory.CreateActiveMemberAsync(tenant);
        var issuer = factory.Services.GetRequiredService<Agendamento.Application.Identity.ISessionIssuer>();
        var session = issuer.Issue(new Agendamento.Application.Identity.CreateSession.SessionPrincipal(userId, tenant, "Admin"));
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.AccessToken);
        using var db = GetDbContext(tenant, "IntegrationTestDb");
        var person = Customer.Create(tenant, "Pessoa fictícia", "11999999999", null, CustomerDocument.Create("CPF", "12345678909"), null);
        var company = Customer.Create(tenant, "Empresa fictícia", "11999999999", null, CustomerDocument.Create("CNPJ", "00000000000191"), null);
        db.Customers.AddRange(person, company);
        await db.SaveChangesAsync();
        foreach (var path in new[] { "/api/v1/customers", $"/api/v1/customers/{person.Id}" })
        {
            var response = await client.GetAsync(path);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            Assert.False(json.Contains(person.Document!.Value), "CPF integral deve permanecer fora da resposta HTTP.");
            using var parsed = System.Text.Json.JsonDocument.Parse(json);
            var item = path.EndsWith(person.Id.ToString()) ? parsed.RootElement :
                parsed.RootElement.GetProperty("items").EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == person.Id);
            Assert.Equal("***.***.***-09", item.GetProperty("documentValue").GetString());
            if (!path.EndsWith(person.Id.ToString()))
                Assert.Equal(company.Document!.Value, parsed.RootElement.GetProperty("items").EnumerateArray()
                    .Single(c => c.GetProperty("id").GetGuid() == company.Id).GetProperty("documentValue").GetString());
        }
        var command = new Agendamento.Api.Application.Customers.UpdateCustomer.UpdateCustomerCommand(
            person.Id, "Nome editado", person.Phone, null, null, null, null, null, null, null, null, null, null, true);
        var updated = await System.Net.Http.Json.HttpClientJsonExtensions.PutAsJsonAsync(client, $"/api/v1/customers/{person.Id}", command);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, updated.StatusCode);
        db.ChangeTracker.Clear();
        var saved = await db.Customers.SingleAsync(c => c.Id == person.Id);
        Assert.Equal("Nome editado", saved.Name);
        Assert.True(saved.Document!.Value == person.Document!.Value, "Editar sem documento deve preservar o valor persistido.");
        var conflicting = await System.Net.Http.Json.HttpClientJsonExtensions.PutAsJsonAsync(client, $"/api/v1/customers/{person.Id}",
            command with { DocumentType = "CPF", DocumentValue = "***.***.***-09" });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, conflicting.StatusCode);
    }
}
