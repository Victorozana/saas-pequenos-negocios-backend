using Agendamento.Api.Domain.Customers;
using Agendamento.Api.Domain.Financial;
using Agendamento.Api.Domain.Notifications;
using Agendamento.Api.Application.Notifications;
using Agendamento.Api.Infrastructure.Notifications;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.IntegrationTests.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Agendamento.Api.Application.Tenancy;
using Xunit;
using static Agendamento.IntegrationTests.Tenancy.PostgreSqlRlsTests;

namespace Agendamento.IntegrationTests.Notifications;

public sealed class OverdueReceivableReminderPostgreSqlTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 12, 0, 1, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Gateway : ICommunicationGateway
    {
        public List<Guid> Attempts { get; } = [];
        public Task<bool> SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
        { Attempts.Add(message.Id); return Task.FromResult(false); }
    }

    private static WebApplicationFactory<Program> Host(Database fixture, Clock clock, Gateway gateway, Guid? tenant = null) =>
        new TeamFinancialTestFactory(fixture.RestrictedConnection, fixture.AdministrativeConnection)
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock);
                services.RemoveAll<ICommunicationGateway>(); services.AddSingleton<ICommunicationGateway>(gateway);
                if (tenant.HasValue) { services.RemoveAll<ITenantContext>(); services.AddScoped<ITenantContext>(_ => new Tenant(tenant.Value)); }
            }));

    private static async Task Run(WebApplicationFactory<Program> host)
    {
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<OverdueReceivableReminderProcessor>().ProcessAsync();
    }

    private static AgendamentoDbContext Admin(Database fixture) => new(
        new DbContextOptionsBuilder<AgendamentoDbContext>().UseNpgsql(fixture.MigrationConnection).Options,
        new Tenant(Guid.Empty));

    [Fact(DisplayName = "Worker dispara ao iniciar e reiniciar sem duplicar; processamento com tenant preserva outra empresa @spec:AC-208")]
    public async Task WorkerStartsAutomaticallyAndTenantScopedRunPreservesOtherCompany()
    {
        await using var fixture = await Database.Create(); var clock = new Clock(); var gateway = new Gateway();
        var titles = new List<ReceivableTitle>();
        await using (var db = Admin(fixture))
        {
            foreach (var tenant in new[] { fixture.A, fixture.B })
            {
                var customer = Customer.Create(tenant, "Cliente worker", "123", null, null, null);
                var title = ReceivableTitle.Create(tenant, "Worker", 100, clock.Now.UtcDateTime.Date.AddDays(-1), customer.Id);
                db.Customers.Add(customer); db.ReceivableTitles.Add(title); titles.Add(title);
            }
            await db.SaveChangesAsync();
        }
        using (var scoped = Host(fixture, clock, gateway, fixture.A)) await Run(scoped);
        await using (var db = Admin(fixture))
        {
            var message = Assert.Single(await db.NotificationMessages.IgnoreQueryFilters().Where(m => m.ReceivableTitleId != null).ToListAsync());
            Assert.Equal(titles[0].Id, message.ReceivableTitleId); Assert.Equal(fixture.A, message.TenantId);
            Assert.Equal(100m, (await db.ReceivableTitles.IgnoreQueryFilters().SingleAsync(t => t.Id == titles[1].Id)).BalanceDue);
        }
        for (var startup = 0; startup < 2; startup++)
        {
            using var host = Host(fixture, clock, gateway);
            using var worker = new OverdueReceivableReminderWorker(host.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<OverdueReceivableReminderWorker>.Instance);
            await worker.StartAsync(CancellationToken.None);
            try
            {
                var deadline = DateTime.UtcNow.AddSeconds(20); var count = 0;
                do
                {
                    await using var db = Admin(fixture);
                    count = await db.NotificationMessages.IgnoreQueryFilters().CountAsync(m => m.ReceivableTitleId != null);
                    if (count == 2) break;
                    await Task.Delay(100);
                } while (DateTime.UtcNow < deadline);
                Assert.Equal(2, count);
            }
            finally { await worker.StopAsync(CancellationToken.None); }
        }
        await using var verify = Admin(fixture);
        var messages = await verify.NotificationMessages.IgnoreQueryFilters().Where(m => m.ReceivableTitleId != null).ToListAsync();
        Assert.Equal(2, messages.Count);
        foreach (var title in titles) Assert.Single(messages, m => m.ReceivableTitleId == title.Id && m.CustomerId == title.CustomerId && m.TenantId == title.TenantId);
        Assert.Empty(gateway.Attempts);
    }

    [Fact(DisplayName = "Aviso diário persistido filtra saldo, vencimento, preferência e vínculo; reinício e dia seguinte preservam isolamento @spec:AC-208")]
    public async Task DailyEligibilityRestartAndOutboxRetries()
    {
        await using var fixture = await Database.Create();
        var clock = new Clock(); var gateway = new Gateway();
        var expected = new List<ReceivableTitle>(); var excluded = new List<ReceivableTitle>();
        await using (var db = Admin(fixture))
        {
            foreach (var tenant in new[] { fixture.A, fixture.B })
            {
                var customer = Customer.Create(tenant, "Cliente fictício", "5511999999999", null, null, null);
                var disabled = Customer.Create(tenant, "Cliente sem lembretes", customer.Phone, null, null, null);
                disabled.SetAutomaticRemindersEnabled(false);
                db.Customers.AddRange(customer, disabled);
                ReceivableTitle Title(string description, int days, Guid? customerId, decimal amount = 100) =>
                    ReceivableTitle.Create(tenant, description, amount, clock.Now.UtcDateTime.Date.AddDays(days), customerId);
                var open = Title("Aberta", -1, customer.Id);
                var partial = Title("Parcial", -2, customer.Id); partial.RegisterPayment(40, PaymentMethod.Cash, clock.Now.UtcDateTime);
                expected.AddRange([open, partial]);
                var paid = Title("Quitada", -3, customer.Id); paid.RegisterPayment(100, PaymentMethod.Cash, clock.Now.UtcDateTime);
                var canceled = Title("Cancelada", -3, customer.Id); canceled.Cancel();
                excluded.AddRange([paid, canceled, Title("Hoje", 0, customer.Id), Title("Futuro", 2, customer.Id),
                    Title("Opt-out", -1, disabled.Id), Title("Sem vínculo, mesmo telefone", -1, null)]);
            }
            db.ReceivableTitles.AddRange(expected.Concat(excluded));
            foreach (var title in expected.Concat(excluded))
                foreach (var payment in title.Payments) db.Entry(payment).Property("TenantId").CurrentValue = title.TenantId;
            await db.SaveChangesAsync();
            // Legacy inconsistent status with zero balance must also be excluded, independent of Paid status.
            var zero = excluded.First(t => t.Description == "Quitada");
            await Execute(fixture.Admin, "UPDATE \"ReceivableTitles\" SET \"Status\"=0 WHERE \"Id\"=@id::uuid", zero.Id.ToString());
        }
        using (var host = Host(fixture, clock, gateway))
        {
            await Run(host); await Run(host);
            Assert.Empty(gateway.Attempts); // Generation only queues; gateway belongs to outbox delivery.
        }
        await using (var db = Admin(fixture))
        {
            var messages = await db.NotificationMessages.IgnoreQueryFilters().Where(m => m.ReceivableTitleId != null).ToListAsync();
            Assert.Equal(4, messages.Count);
            foreach (var title in expected)
            {
                var message = Assert.Single(messages, m => m.ReceivableTitleId == title.Id);
                Assert.Equal(title.TenantId, message.TenantId); Assert.Equal(title.CustomerId, message.CustomerId);
                Assert.Equal(NotificationAudience.Customer, message.Audience);
                Assert.Equal(new DateOnly(2026, 10, 12), message.ReminderDate);
                Assert.Equal(NotificationStatus.Pending, message.Status); Assert.Equal(0, message.RetryCount);
                Assert.Contains(title.Description == "Parcial" ? "60,00" : "100,00", message.Content);
            }
            Assert.DoesNotContain(messages, m => excluded.Any(t => t.Id == m.ReceivableTitleId));
        }
        await using (var duplicateDb = Admin(fixture))
        {
            var title = expected[0];
            var duplicate = NotificationMessage.Create(title.TenantId, "123", NotificationChannel.WhatsApp,
                "Tentativa duplicada", title.CustomerId, NotificationAudience.Customer);
            duplicateDb.NotificationMessages.Add(duplicate);
            duplicateDb.Entry(duplicate).Property(m => m.ReceivableTitleId).CurrentValue = title.Id;
            duplicateDb.Entry(duplicate).Property(m => m.ReminderDate).CurrentValue = new DateOnly(2026, 10, 12);
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => duplicateDb.SaveChangesAsync());
            var postgres = Assert.IsType<Npgsql.PostgresException>(error.InnerException);
            Assert.Equal(Npgsql.PostgresErrorCodes.UniqueViolation, postgres.SqlState);
            Assert.Equal("UX_NotificationMessages_DailyReceivableReminder", postgres.ConstraintName);
        }
        foreach (var invalid in new[] { (Title: expected[2].Id, Customer: expected[0].CustomerId),
            (Title: expected[0].Id, Customer: expected[2].CustomerId) })
        {
            await using var invalidDb = Admin(fixture);
            var message = NotificationMessage.Create(fixture.A, "123", NotificationChannel.WhatsApp,
                "Vínculo externo inválido", invalid.Customer, NotificationAudience.Customer);
            invalidDb.NotificationMessages.Add(message);
            invalidDb.Entry(message).Property(m => m.ReceivableTitleId).CurrentValue = invalid.Title;
            invalidDb.Entry(message).Property(m => m.ReminderDate).CurrentValue = new DateOnly(2026, 10, 13);
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => invalidDb.SaveChangesAsync());
            Assert.Equal(Npgsql.PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<Npgsql.PostgresException>(error.InnerException).SqlState);
        }
        using (var restarted = Host(fixture, clock, gateway))
        {
            await Run(restarted);
            using var scope = restarted.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<INotificationService>().ProcessPendingAsync();
            await Run(restarted);
        }
        await using (var db = Admin(fixture))
        {
            var messages = await db.NotificationMessages.IgnoreQueryFilters().Where(m => m.ReceivableTitleId != null).ToListAsync();
            Assert.Equal(4, messages.Count); Assert.All(messages, m => Assert.Equal(1, m.RetryCount));
            Assert.All(messages, m => Assert.Contains(m.Id, gateway.Attempts));
            var paid = await db.ReceivableTitles.IgnoreQueryFilters().Include(t => t.Payments).SingleAsync(t => t.Id == expected[0].Id);
            paid.RegisterPayment(paid.BalanceDue, PaymentMethod.Cash, clock.Now.UtcDateTime);
            db.ChangeTracker.DetectChanges();
            foreach (var payment in paid.Payments)
            {
                db.Entry(payment).State = EntityState.Added;
                db.Entry(payment).Property("TenantId").CurrentValue = paid.TenantId;
            }
            var canceled = await db.ReceivableTitles.IgnoreQueryFilters().SingleAsync(t => t.Id == expected[1].Id); canceled.Cancel();
            await db.SaveChangesAsync();
        }
        clock.Now = clock.Now.AddDays(1);
        using (var nextDay = Host(fixture, clock, gateway)) await Run(nextDay);
        await using (var db = Admin(fixture))
        {
            var tomorrow = await db.NotificationMessages.IgnoreQueryFilters().Where(m => m.ReminderDate == new DateOnly(2026, 10, 13)).ToListAsync();
            // The title due yesterday (previously due today) becomes eligible too, in each company.
            Assert.Equal(4, tomorrow.Count);
            Assert.DoesNotContain(tomorrow, m => m.ReceivableTitleId == expected[0].Id || m.ReceivableTitleId == expected[1].Id);
            foreach (var title in expected.Where(t => t.TenantId == fixture.B))
                Assert.Single(tomorrow, m => m.ReceivableTitleId == title.Id && m.CustomerId == title.CustomerId && m.TenantId == fixture.B);
        }
    }

    [Fact(DisplayName = "Dois workers simultâneos usam deduplicação durável PostgreSQL por conta e dia @spec:AC-208")]
    public async Task ConcurrentWorkersWaitOnSameAccountWithoutDuplicating()
    {
        await using var fixture = await Database.Create(); var clock = new Clock(); var gateway = new Gateway();
        ReceivableTitle title;
        await using (var db = Admin(fixture))
        {
            var customer = Customer.Create(fixture.A, "Cliente concorrência", "123", null, null, null);
            title = ReceivableTitle.Create(fixture.A, "Concorrente", 100, clock.Now.UtcDateTime.Date.AddDays(-1), customer.Id);
            db.Customers.Add(customer); db.ReceivableTitles.Add(title); await db.SaveChangesAsync();
        }
        using var host = Host(fixture, clock, gateway);
        await using var blocker = await fixture.Admin.BeginTransactionAsync();
        await Execute(fixture.Admin, "SELECT 1 FROM \"ReceivableTitles\" WHERE \"Id\"=@id::uuid FOR UPDATE", title.Id.ToString());
        var workers = new[] { Run(host), Run(host) }; long waiting = 0;
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(15);
            do
            {
                await Execute(fixture.Admin, "SELECT pg_stat_clear_snapshot()");
                waiting = (long)(await Scalar(fixture.Admin, "SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND pid<>pg_backend_pid() AND query LIKE '%FOR UPDATE%' AND wait_event_type='Lock'"))!;
                if (waiting == 2) break;
                await Task.Delay(100);
            } while (DateTime.UtcNow < deadline);
        }
        finally { await blocker.RollbackAsync(); }
        await Task.WhenAll(workers); Assert.Equal(2L, waiting);
        await using var verify = Admin(fixture);
        var message = Assert.Single(await verify.NotificationMessages.IgnoreQueryFilters().Where(m => m.ReceivableTitleId == title.Id).ToListAsync());
        Assert.Equal(fixture.A, message.TenantId); Assert.Equal(title.CustomerId, message.CustomerId);
        Assert.Equal(new DateOnly(2026, 10, 12), message.ReminderDate); Assert.Empty(gateway.Attempts);
    }

    [Fact(DisplayName = "Migration de deduplicação preserva histórico na base temporária exclusiva @spec:AC-208")]
    public async Task MigrationPreservesHistoricalNotificationRows()
    {
        await using var fixture = await Database.Create(migrationTarget: "20261012174238_AddCustomerAutomaticReminderPreference");
        var before = (string)(await Scalar(fixture.Admin, "SELECT jsonb_agg(to_jsonb(n) ORDER BY \"Id\")::text FROM \"NotificationMessages\" n"))!;
        await using (var db = Admin(fixture)) { await db.Database.MigrateAsync(); Assert.Empty(await db.Database.GetPendingMigrationsAsync()); }
        var after = (string)(await Scalar(fixture.Admin, "SELECT jsonb_agg(to_jsonb(n)-'ReceivableTitleId'-'ReminderDate' ORDER BY \"Id\")::text FROM \"NotificationMessages\" n"))!;
        Assert.Equal(before, after);
        Assert.Equal(2L, await Scalar(fixture.Admin, "SELECT count(*) FROM \"NotificationMessages\" WHERE \"ReceivableTitleId\" IS NULL AND \"ReminderDate\" IS NULL"));
        await using (var system = new Npgsql.NpgsqlConnection(fixture.AdministrativeConnection))
        {
            await system.OpenAsync(); await Execute(system, "SET ROLE agendamento_system_user");
            Assert.Equal(2L, await Scalar(system, "SELECT count(*) FROM \"ReceivableTitles\""));
            await using var transaction = await system.BeginTransactionAsync();
            await Execute(system, "SELECT \"Id\" FROM \"ReceivableTitles\" FOR UPDATE");
            await Execute(system, "SAVEPOINT forbidden_write");
            var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => Execute(system, "UPDATE \"ReceivableTitles\" SET \"Id\"=\"Id\""));
            Assert.Equal(Npgsql.PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
            await Execute(system, "ROLLBACK TO SAVEPOINT forbidden_write");
            await transaction.RollbackAsync();
        }
        await using (var restricted = new Npgsql.NpgsqlConnection(fixture.RestrictedConnection))
        {
            await restricted.OpenAsync(); await using var transaction = await restricted.BeginTransactionAsync();
            await Execute(restricted, "SELECT set_config('app.tenant_id', @id, true)", fixture.A.ToString());
            Assert.Equal(1L, await Scalar(restricted, "SELECT count(*) FROM \"ReceivableTitles\""));
            Assert.Equal(0L, await Scalar(restricted, "SELECT count(*) FROM \"ReceivableTitles\" WHERE tenant_id=@id::uuid", fixture.B.ToString()));
            await transaction.RollbackAsync();
        }
    }
}
