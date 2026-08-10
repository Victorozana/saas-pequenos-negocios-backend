using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agendamento.Api.Application.Common;
using Agendamento.Api.Application.Customers.CreateCustomer;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Agendamento.UnitTests.Customers;

public class CustomerHandlerTests
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

    private class DummyUnitOfWork : IUnitOfWork
    {
        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
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

    [Fact(DisplayName = "Deve criar cliente se dados validos")]
    public async Task CreateCustomer_ShouldAddCustomerToDatabase()
    {
        var tenantId = Guid.NewGuid();
        var db = GetDbContext(tenantId, Guid.NewGuid().ToString());
        var handler = new CreateCustomerHandler(db, new DummyTenantContext(tenantId), new DummyUnitOfWork());

        var command = new CreateCustomerCommand(
            "John Doe", "11999999999", "john@doe.com", "CPF", "12345678909",
            "Main St", "123", null, "Downtown", "City", "ST", "12345");

        var id = await handler.HandleAsync(command);

        Assert.NotEqual(Guid.Empty, id);
        
        var savedCustomer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id);
        Assert.NotNull(savedCustomer);
        Assert.Equal("John Doe", savedCustomer.Name);
        Assert.Equal("CPF", savedCustomer.Document?.Type);
        Assert.Equal(tenantId, savedCustomer.TenantId);
    }

    [Fact(DisplayName = "Deve validar formato de CPF/CNPJ @spec:AC-042")]
    public void CreateCustomerDocument_ShouldValidateFormat()
    {
        var exCpf = Assert.Throws<ArgumentException>(() => Agendamento.Api.Domain.Customers.CustomerDocument.Create("CPF", "123"));
        Assert.Contains("exactly 11 digits", exCpf.Message);

        var exCnpj = Assert.Throws<ArgumentException>(() => Agendamento.Api.Domain.Customers.CustomerDocument.Create("CNPJ", "123"));
        Assert.Contains("exactly 14 digits", exCnpj.Message);

        var validCpf = Agendamento.Api.Domain.Customers.CustomerDocument.Create("CPF", "12345678909");
        Assert.Equal("12345678909", validCpf.Value);
    }

    [Fact(DisplayName = "Busca deve permitir filtro parcial e paginacao @spec:AC-043")]
    public async Task GetCustomers_ShouldSupportPaginationAndFiltering()
    {
        var tenantId = Guid.NewGuid();
        var db = GetDbContext(tenantId, Guid.NewGuid().ToString());
        
        db.Customers.Add(Agendamento.Api.Domain.Customers.Customer.Create(tenantId, "Alice Silva", "11999999999", null, null, null));
        db.Customers.Add(Agendamento.Api.Domain.Customers.Customer.Create(tenantId, "Bob Santos", "11888888888", null, null, null));
        db.Customers.Add(Agendamento.Api.Domain.Customers.Customer.Create(tenantId, "Charlie Silva", "11777777777", null, null, null));
        await db.SaveChangesAsync();

        var handler = new Agendamento.Api.Application.Customers.GetCustomers.GetCustomersHandler(db, new DummyTenantContext(tenantId));

        var resultPage1 = await handler.HandleAsync(search: "silva", page: 1, pageSize: 1);
        Assert.Equal(2, resultPage1.TotalCount); // Alice e Charlie têm "silva"
        Assert.Single(resultPage1.Items); // pageSize = 1
        Assert.Equal(1, resultPage1.Page);

        var resultPage2 = await handler.HandleAsync(search: "silva", page: 2, pageSize: 1);
        Assert.Single(resultPage2.Items); // pageSize = 1
        Assert.NotEqual(resultPage1.Items.First().Id, resultPage2.Items.First().Id);
    }
}
