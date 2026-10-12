using Agendamento.Api.Application.Appointments.CreateAppointment;
using Agendamento.Api.Application.Common;
using Agendamento.Api.Application.Notifications;
using Agendamento.Api.Application.Notifications.ScheduleAppointmentReminder;
using Agendamento.Api.Application.Notifications.SendQuotationNotification;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Appointments;
using Agendamento.Api.Domain.Customers;
using Agendamento.Api.Domain.Notifications;
using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Domain.WorkOrders;
using Agendamento.Api.Infrastructure.Notifications;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Agendamento.IntegrationTests.Notifications;

public sealed class AutomaticReminderPreferenceTests
{
    private sealed class Tenant(Guid id) : ITenantContext { public Guid TenantId => id; public bool HasTenant => id != Guid.Empty; }
    private sealed class UnitOfWork : IUnitOfWork
    {
        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class Gateway : ICommunicationGateway
    {
        public List<Guid> Delivered { get; } = [];
        public Task<bool> SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
        { Delivered.Add(message.Id); return Task.FromResult(true); }
    }
    private static AgendamentoDbContext Db(Guid tenant, string name) => new(new DbContextOptionsBuilder<AgendamentoDbContext>()
        .UseInMemoryDatabase(name).AddInterceptors(new TenantSaveChangesInterceptor(new Tenant(tenant))).Options, new Tenant(tenant));

    [Theory(DisplayName = "Desativar cliente impede ambos os lembretes automáticos e preserva outros clientes e empresas @spec:AC-207")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BothAutomaticPathsRespectPreferenceAndTenant(bool scheduledReminder)
    {
        var database = Guid.NewGuid().ToString(); var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var gateway = new Gateway(); var expected = new List<Guid>(); var disabledId = Guid.Empty;
        foreach (var scenario in new[] { (Tenant: a, Enabled: false), (Tenant: a, Enabled: true), (Tenant: b, Enabled: true) })
        {
            var deliveredBeforeQueue = gateway.Delivered.Count;
            await using var db = Db(scenario.Tenant, database);
            var customer = Customer.Create(scenario.Tenant, "Cliente fictício", "5511999999999", null, null, null);
            Assert.True(customer.AutomaticRemindersEnabled);
            customer.SetAutomaticRemindersEnabled(scenario.Enabled);
            if (!scenario.Enabled) disabledId = customer.Id;
            var quotation = Quotation.Create(scenario.Tenant, "Q-PREFERENCE", customer.Id, DateTime.UtcNow, DateTime.UtcNow.AddDays(10), null, null);
            quotation.AddItem(QuotationItem.Create(null, "Serviço", "un", 100m, 1m, 0m)); quotation.MarkAsPending(); quotation.Approve();
            var order = WorkOrder.CreateFromQuotation(quotation);
            db.Customers.Add(customer); db.Quotations.Add(quotation); db.WorkOrders.Add(order); await db.SaveChangesAsync();
            var notifications = new NotificationService(db, new Tenant(scenario.Tenant), gateway, NullLogger<NotificationService>.Instance);
            if (scheduledReminder)
            {
                var appointment = Appointment.Create(scenario.Tenant, order.Id, AppointmentType.Installation, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(1), "Endereço", null, null);
                db.Appointments.Add(appointment); await db.SaveChangesAsync();
                Assert.Equal(scenario.Enabled, await new ScheduleAppointmentReminderHandler(db, notifications).HandleAsync(new(appointment.Id)));
            }
            else
            {
                var appointmentId = await new CreateAppointmentHandler(db, new Tenant(scenario.Tenant), new UnitOfWork(), notifications).HandleAsync(new CreateAppointmentCommand
                { WorkOrderId = order.Id, Type = AppointmentType.Installation, StartTime = DateTime.UtcNow.AddDays(1), EndTime = DateTime.UtcNow.AddDays(1).AddHours(1), Address = "Endereço" });
                Assert.True(await db.Appointments.AnyAsync(p => p.Id == appointmentId));
            }
            Assert.Equal(deliveredBeforeQueue, gateway.Delivered.Count);
            var automatic = await db.NotificationMessages.ToListAsync();
            if (scenario.Enabled)
            {
                var message = Assert.Single(automatic, m => m.CustomerId == customer.Id);
                Assert.Equal(scenario.Tenant, message.TenantId); expected.Add(message.Id);
            }
            else Assert.Empty(automatic);
            await notifications.ProcessPendingAsync();
        }
        Assert.Equal(expected.Order(), gateway.Delivered.Order());
        await using var verify = Db(a, database);
        Assert.DoesNotContain(await verify.NotificationMessages.IgnoreQueryFilters().ToListAsync(), m => m.CustomerId == disabledId);
        Assert.Equal(2, await verify.NotificationMessages.IgnoreQueryFilters().CountAsync());
        Assert.False((await verify.Customers.SingleAsync(c => c.Id == disabledId)).AutomaticRemindersEnabled);
    }

    [Fact(DisplayName = "Lembrete automático rejeita cliente externo, ausente e contexto sem empresa antes de enfileirar @spec:AC-207")]
    public async Task AutomaticQueueRejectsInvalidCustomerAndTenantWithoutStaging()
    {
        var name = Guid.NewGuid().ToString(); var own = Guid.NewGuid(); var other = Guid.NewGuid();
        var foreign = Customer.Create(other, "Cliente externo fictício", "123", null, null, null);
        await using (var seed = Db(other, name)) { seed.Customers.Add(foreign); await seed.SaveChangesAsync(); }
        await using var db = Db(own, name); var gateway = new Gateway();
        var service = new NotificationService(db, new Tenant(own), gateway, NullLogger<NotificationService>.Instance);
        foreach (var id in new[] { foreign.Id, Guid.NewGuid(), Guid.Empty })
            await Assert.ThrowsAsync<ArgumentException>(() => service.QueueAutomaticReminderAsync(id, "123", NotificationChannel.WhatsApp, "Inválida"));
        var anonymous = new NotificationService(db, new Tenant(Guid.Empty), gateway, NullLogger<NotificationService>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => anonymous.QueueAutomaticReminderAsync(foreign.Id, "123", NotificationChannel.WhatsApp, "Inválida"));
        Assert.Empty(db.ChangeTracker.Entries<NotificationMessage>());
        Assert.Empty(await db.NotificationMessages.IgnoreQueryFilters().ToListAsync()); Assert.Empty(gateway.Delivered);
    }

    [Fact(DisplayName = "Preferência de lembretes não bloqueia mensagem manual, orçamento ou convite System @spec:AC-207")]
    public async Task DisabledRemindersPreserveOtherMessages()
    {
        var tenant = Guid.NewGuid(); await using var db = Db(tenant, Guid.NewGuid().ToString());
        var customer = Customer.Create(tenant, "Cliente fictício", "123", null, null, null); customer.SetAutomaticRemindersEnabled(false);
        var quotation = Quotation.Create(tenant, "Q-PREFERENCE", customer.Id, DateTime.UtcNow, DateTime.UtcNow.AddDays(10), null, null);
        db.Customers.Add(customer); db.Quotations.Add(quotation); await db.SaveChangesAsync();
        var gateway = new Gateway(); var service = new NotificationService(db, new Tenant(tenant), gateway, NullLogger<NotificationService>.Instance);
        Assert.Null(await service.QueueAutomaticReminderAsync(customer.Id, customer.Phone, NotificationChannel.WhatsApp, "Automático"));
        var manual = await service.QueueForCustomerAsync(customer.Id, customer.Phone, NotificationChannel.WhatsApp, "Manual");
        Assert.True(await new SendQuotationNotificationHandler(db, service).HandleAsync(new(quotation.Id)));
        var invite = service.QueueSystem(customer.Phone, NotificationChannel.Email, "Convite");
        await db.SaveChangesAsync(); await service.ProcessPendingAsync();
        var messages = await db.NotificationMessages.ToListAsync(); Assert.Equal(3, messages.Count);
        Assert.All(messages, message => Assert.Equal(NotificationStatus.Sent, message.Status));
        Assert.Contains(manual.Id, gateway.Delivered); Assert.Contains(invite.Id, gateway.Delivered);
        Assert.Contains(messages, m => m.CustomerId == customer.Id && m.Content.Contains("Q-PREFERENCE"));
        Assert.Equal(NotificationAudience.System, invite.Audience); Assert.Null(invite.CustomerId);
    }
}
