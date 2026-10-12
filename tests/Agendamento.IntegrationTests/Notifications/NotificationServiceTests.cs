using System.Reflection;
using Agendamento.Api.Application.Notifications;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Customers;
using Agendamento.Api.Domain.Notifications;
using Agendamento.Api.Infrastructure.Notifications;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Agendamento.IntegrationTests.Notifications;

public sealed class NotificationServiceTests
{
    private sealed class Tenant(Guid id) : ITenantContext { public Guid TenantId => id; public bool HasTenant => id != Guid.Empty; }
    private sealed class Gateway : ICommunicationGateway
    {
        public List<Guid> Sent { get; } = [];
        public bool Success { get; set; } = true;
        public bool Throw { get; set; }
        public Task<bool> SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
        {
            Sent.Add(message.Id);
            if (Throw) throw new InvalidOperationException("provider-secret-must-not-persist");
            return Task.FromResult(Success);
        }
    }
    private static AgendamentoDbContext Db(Guid tenant, string name) => new(new DbContextOptionsBuilder<AgendamentoDbContext>()
        .UseInMemoryDatabase(name).AddInterceptors(new TenantSaveChangesInterceptor(new Tenant(tenant))).Options, new Tenant(tenant));
    private static NotificationService Service(AgendamentoDbContext db, Guid tenant, ICommunicationGateway gateway) =>
        new(db, new Tenant(tenant), gateway, NullLogger<NotificationService>.Instance);

    [Theory(DisplayName = "Serviço central enfileira antes de enviar e aceita troca do provedor @spec:AC-097 @spec:AC-098")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueueAndDispatchAreSeparatedAndProviderCanBeReplaced(bool simulated)
    {
        var name = Guid.NewGuid().ToString(); var tenant = Guid.NewGuid();
        await using var db = Db(tenant, name);
        var customer = Customer.Create(tenant, "Cliente", "123", null, null, null);
        db.Customers.Add(customer); await db.SaveChangesAsync();
        var gateway = new Gateway();
        ICommunicationGateway provider = simulated ? new MockCommunicationGateway(NullLogger<MockCommunicationGateway>.Instance) : gateway;
        INotificationService service = Service(db, tenant, provider);
        var queued = await service.QueueForCustomerAsync(customer.Id, customer.Phone!, NotificationChannel.WhatsApp, "Mensagem");
        Assert.Empty(gateway.Sent);
        Assert.Equal(NotificationStatus.Pending, queued.Status);
        await using (var beforeCommit = Db(tenant, name)) Assert.Empty(await beforeCommit.NotificationMessages.ToListAsync());
        await db.SaveChangesAsync();
        await service.ProcessPendingAsync();
        await using var verification = Db(tenant, name);
        var sent = await verification.NotificationMessages.SingleAsync();
        Assert.Equal(queued.Id, sent.Id); Assert.Equal(tenant, sent.TenantId); Assert.Equal(customer.Id, sent.CustomerId);
        Assert.Equal(NotificationAudience.Customer, sent.Audience); Assert.Equal(NotificationStatus.Sent, sent.Status);
        Assert.NotNull(sent.SentAt); Assert.Equal(0, sent.RetryCount);
        if (!simulated) Assert.Equal(new[] { queued.Id }, gateway.Sent);
    }

    [Fact(DisplayName = "Lembrete e orçamento usam serviço central mantendo templates e cliente @spec:AC-097 @spec:AC-098 @spec:AC-062")]
    public async Task BusinessHandlersKeepTemplatesAndCustomerProvenance()
    {
        var tenant = Guid.NewGuid(); await using var db = Db(tenant, Guid.NewGuid().ToString());
        var customer = Customer.Create(tenant, "Cliente Template", "5511999999999", "example@example.test", null, null);
        var quotation = Agendamento.Api.Domain.Quotations.Quotation.Create(tenant, "Q-HISTORY", customer.Id, DateTime.UtcNow, DateTime.UtcNow.AddDays(10), null, null);
        quotation.AddItem(Agendamento.Api.Domain.Quotations.QuotationItem.Create(null, "Serviço", "un", 100m, 1m, 0m));
        quotation.MarkAsPending(); quotation.Approve();
        var order = Agendamento.Api.Domain.WorkOrders.WorkOrder.CreateFromQuotation(quotation);
        var appointment = Agendamento.Api.Domain.Appointments.Appointment.Create(tenant, order.Id,
            Agendamento.Api.Domain.Appointments.AppointmentType.Installation, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(1), "Endereço", null, null);
        db.Customers.Add(customer); db.Quotations.Add(quotation); db.WorkOrders.Add(order); db.Appointments.Add(appointment); await db.SaveChangesAsync();
        var gateway = new Gateway(); var service = Service(db, tenant, gateway);
        var reminder = new Agendamento.Api.Application.Notifications.ScheduleAppointmentReminder.ScheduleAppointmentReminderHandler(db, service);
        var quote = new Agendamento.Api.Application.Notifications.SendQuotationNotification.SendQuotationNotificationHandler(db, service);
        Assert.True(await reminder.HandleAsync(new(appointment.Id)));
        Assert.True(await quote.HandleAsync(new(quotation.Id)));
        Assert.False(await reminder.HandleAsync(new(Guid.NewGuid()))); Assert.False(await quote.HandleAsync(new(Guid.NewGuid())));
        Assert.Empty(gateway.Sent);
        var messages = await db.NotificationMessages.ToListAsync(); Assert.Equal(2, messages.Count);
        Assert.All(messages, m => { Assert.Equal(customer.Id, m.CustomerId); Assert.Equal(tenant, m.TenantId);
            Assert.Equal(NotificationAudience.Customer, m.Audience); Assert.Equal(NotificationStatus.Pending, m.Status);
            Assert.Contains(customer.Name, m.Content); Assert.DoesNotContain("{{", m.Content); Assert.DoesNotContain("Token", m.Content); });
        Assert.Contains(messages, m => m.Content.Contains("Q-HISTORY"));
        Assert.Contains(messages, m => m.Content.Contains(appointment.StartTime.ToString("dd/MM/yyyy")));
    }

    [Fact(DisplayName = "Serviço rejeita cliente externo, inexistente e tenant ausente antes de gravar @spec:AC-098")]
    public async Task RejectInvalidCustomerLinksBeforeStaging()
    {
        var name = Guid.NewGuid().ToString(); var tenant = Guid.NewGuid(); var other = Guid.NewGuid();
        Guid foreignId;
        await using (var seed = Db(other, name))
        {
            var customer = Customer.Create(other, "Outro", "123", null, null, null);
            foreignId = customer.Id; seed.Customers.Add(customer); await seed.SaveChangesAsync();
        }
        await using var db = Db(tenant, name); var gateway = new Gateway(); var service = Service(db, tenant, gateway);
        foreach (var id in new[] { foreignId, Guid.NewGuid(), Guid.Empty })
            await Assert.ThrowsAsync<ArgumentException>(() => service.QueueForCustomerAsync(id, "123", NotificationChannel.Email, "Teste"));
        Assert.Empty(db.ChangeTracker.Entries<NotificationMessage>());
        Assert.Empty(await db.NotificationMessages.IgnoreQueryFilters().ToListAsync()); Assert.Empty(gateway.Sent);
        var anonymous = Service(db, Guid.Empty, gateway);
        Assert.Throws<InvalidOperationException>(() => anonymous.QueueStandalone("123", NotificationChannel.Email, "Teste"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => anonymous.QueueForCustomerAsync(foreignId, "123", NotificationChannel.Email, "Teste"));
    }

    [Fact(DisplayName = "Worker usa serviço central para tenants distintos, retries, cancelamentos e backoff @spec:AC-097 @spec:AC-063 @spec:AC-061")]
    public async Task RealWorkerPreservesRetryAndBatchEligibility()
    {
        var name = Guid.NewGuid().ToString(); var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var gateway = new Gateway { Success = false }; Guid firstId, secondId, cancelledId, delayedId;
        await using (var seed = Db(a, name))
        {
            var service = Service(seed, a, gateway);
            firstId = service.QueueStandalone("123", NotificationChannel.Email, "Primeira").Id;
            var cancelled = service.QueueStandalone("123", NotificationChannel.Email, "Cancelada"); cancelled.Cancel(); cancelledId = cancelled.Id;
            var delayed = service.QueueStandalone("123", NotificationChannel.Email, "Adiada"); delayed.MarkAsFailed("Retry"); delayedId = delayed.Id;
            await seed.SaveChangesAsync();
        }
        await using (var seed = Db(b, name))
        {
            secondId = Service(seed, b, gateway).QueueSystem("123", NotificationChannel.Email, "Sistema").Id; await seed.SaveChangesAsync();
        }
        var services = new ServiceCollection(); services.AddLogging();
        services.AddScoped(_ => Db(Guid.Empty, name)); services.AddScoped<ITenantContext>(_ => new Tenant(Guid.Empty));
        services.AddSingleton<ICommunicationGateway>(gateway); services.AddScoped<INotificationService, NotificationService>();
        using var provider = services.BuildServiceProvider();
        using var worker = new NotificationOutboxProcessor(provider, NullLogger<NotificationOutboxProcessor>.Instance);
        var method = typeof(NotificationOutboxProcessor).GetMethod("ProcessOutboxMessagesAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(worker, new object[] { CancellationToken.None })!;
        Assert.Equal(new[] { firstId, secondId }.Order(), gateway.Sent.Order());
        await using var db = Db(Guid.Empty, name);
        var messages = await db.NotificationMessages.IgnoreQueryFilters().ToDictionaryAsync(m => m.Id);
        foreach (var id in new[] { firstId, secondId })
        {
            Assert.Equal(NotificationStatus.Pending, messages[id].Status); Assert.Equal(1, messages[id].RetryCount);
            Assert.InRange(messages[id].NextRetryAt!.Value, DateTime.UtcNow.AddMinutes(1.8), DateTime.UtcNow.AddMinutes(2.1));
        }
        Assert.Equal(NotificationStatus.Cancelled, messages[cancelledId].Status); Assert.Equal(1, messages[delayedId].RetryCount);
        // Force eligibility in the fixture, preserving the retry count. Exercise provider exception then permanent failure.
        foreach (var retry in new[] { 2, 3 })
        {
            foreach (var tenant in new[] { a, b })
            {
                await using var update = Db(tenant, name);
                var message = await update.NotificationMessages.SingleAsync(m => m.Id == (tenant == a ? firstId : secondId));
                update.Entry(message).Property(m => m.NextRetryAt).CurrentValue = DateTime.UtcNow.AddSeconds(-1);
                await update.SaveChangesAsync();
            }
            gateway.Throw = true;
            await (Task)method.Invoke(worker, new object[] { CancellationToken.None })!;
            await using var check = Db(a, name);
            var result = await check.NotificationMessages.SingleAsync(m => m.Id == firstId);
            Assert.Equal(retry, result.RetryCount); Assert.DoesNotContain("secret", result.LastError!);
            Assert.Equal(retry == 3 ? NotificationStatus.Failed : NotificationStatus.Pending, result.Status);
            if (retry == 3) Assert.Null(result.NextRetryAt);
            else Assert.InRange(result.NextRetryAt!.Value, DateTime.UtcNow.AddMinutes(3.8), DateTime.UtcNow.AddMinutes(4.1));
        }
        var count = gateway.Sent.Count;
        await (Task)method.Invoke(worker, new object[] { CancellationToken.None })!;
        Assert.Equal(count, gateway.Sent.Count);
    }
}
