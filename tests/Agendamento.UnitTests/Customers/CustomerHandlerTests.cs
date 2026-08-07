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

    [Fact]
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
}
