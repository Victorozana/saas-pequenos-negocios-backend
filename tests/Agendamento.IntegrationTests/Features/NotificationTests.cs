using Agendamento.Api.Infrastructure.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agendamento.Api.Application.Appointments.CreateAppointment;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Appointments;
using Agendamento.Api.Domain.Customers;
using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Domain.WorkOrders;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Agendamento.IntegrationTests.Features;

public class NotificationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateAppointment_WithForeignOrMissingWorkOrder_ShouldRejectWithoutWrites(bool seedForeignOrder)
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var workOrderId = Guid.NewGuid();
        if (seedForeignOrder)
        {
            var foreignTenant = Guid.NewGuid();
            await using var seed = GetDbContext(foreignTenant, dbName);
            var quotation = Quotation.Create(foreignTenant, "OTHER", Guid.NewGuid(), DateTime.UtcNow, null, null, null);
            quotation.AddItem(QuotationItem.Create(null, "Serviço", "Unidade", 100m, 1m, 0m));
            quotation.MarkAsPending();
            quotation.Approve();
            var order = WorkOrder.CreateFromQuotation(quotation);
            workOrderId = order.Id;
            seed.Quotations.Add(quotation);
            seed.WorkOrders.Add(order);
            await seed.SaveChangesAsync();
        }

        await using var db = GetDbContext(tenantId, dbName);
        var handler = new CreateAppointmentHandler(db, new DummyTenantContext(tenantId), new UnitOfWork(db), new NotificationService(db, new DummyTenantContext(tenantId), new MockCommunicationGateway(NullLogger<MockCommunicationGateway>.Instance), NullLogger<NotificationService>.Instance));
        var command = new CreateAppointmentCommand
        {
            WorkOrderId = workOrderId,
            Type = AppointmentType.Installation,
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddHours(1),
            Address = "Test"
        };

        var error = await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command));
        Assert.Equal("A ordem de serviço informada não foi encontrada.", error.Message);
        Assert.Empty(db.ChangeTracker.Entries<Appointment>());
        Assert.False(await db.Appointments.IgnoreQueryFilters().AnyAsync());
        Assert.False(await db.NotificationMessages.IgnoreQueryFilters().AnyAsync());
    }

    [Fact]
    public async Task CreateAppointment_WithoutWorkOrder_ShouldSucceed()
    {
        var tenantId = Guid.NewGuid();
        await using var db = GetDbContext(tenantId, Guid.NewGuid().ToString());
        var handler = new CreateAppointmentHandler(db, new DummyTenantContext(tenantId), new UnitOfWork(db), new NotificationService(db, new DummyTenantContext(tenantId), new MockCommunicationGateway(NullLogger<MockCommunicationGateway>.Instance), NullLogger<NotificationService>.Instance));
        var id = await handler.HandleAsync(new CreateAppointmentCommand
        {
            Type = AppointmentType.Installation,
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddHours(1),
            Address = "Test"
        });
        Assert.Equal(id, (await db.Appointments.SingleAsync()).Id);
        Assert.False(await db.NotificationMessages.AnyAsync());
    }

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

    [Fact(DisplayName = "Despacha notificação ao agendar quando OS tem cliente @spec:AC-099")]
    public async Task CreateAppointment_WithWorkOrder_ShouldCreateNotificationMessage()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var db = GetDbContext(tenantId, dbName);
        var tenantContext = new DummyTenantContext(tenantId);
        
        var customer = Customer.Create(tenantId, "Client With Phone", "5511999999999", "client@mail.com", null, null);
        var quotation = Quotation.Create(tenantId, "Q1", customer.Id, DateTime.UtcNow, null, null, null);
        quotation.AddItem(QuotationItem.Create(Guid.NewGuid(), "Instalação", "Unidade", 100m, 1m, 0m));
        quotation.MarkAsPending();
        quotation.Approve();
        var workOrder = WorkOrder.CreateFromQuotation(quotation);

        db.Customers.Add(customer);
        db.Quotations.Add(quotation);
        db.WorkOrders.Add(workOrder);
        await db.SaveChangesAsync();

        var db2 = GetDbContext(tenantId, dbName);
        var unitOfWork = new UnitOfWork(db2);
        var handler = new CreateAppointmentHandler(db2, tenantContext, unitOfWork, new NotificationService(db2, tenantContext, new MockCommunicationGateway(NullLogger<MockCommunicationGateway>.Instance), NullLogger<NotificationService>.Instance));
        
        var command = new CreateAppointmentCommand
        {
            WorkOrderId = workOrder.Id,
            Type = AppointmentType.Installation,
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddHours(2),
            Address = "Test Address"
        };

        // Act
        await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        var db3 = GetDbContext(tenantId, dbName);
        var notification = await db3.NotificationMessages.IgnoreQueryFilters().FirstOrDefaultAsync();
        Assert.NotNull(notification);
        Assert.Equal("5511999999999", notification.Recipient);
        Assert.Contains("Test Address", notification.Content);
        Assert.Equal(tenantId, notification.TenantId);
        Assert.Equal(customer.Id, notification.CustomerId);
        Assert.Equal(Agendamento.Api.Domain.Notifications.NotificationAudience.Customer, notification.Audience);
    }
}
