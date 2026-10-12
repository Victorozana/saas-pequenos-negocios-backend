using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.IntegrationTests.Tenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Agendamento.Api.Application.Notifications;
using Agendamento.Api.Features.Notifications;
using Agendamento.IntegrationTests.Features;
using static Agendamento.IntegrationTests.Tenancy.PostgreSqlRlsTests;

namespace Agendamento.IntegrationTests.Notifications;

public sealed class NotificationHistoryPostgreSqlTests
{
    [Theory(DisplayName = "Migration preserva histórico sem inventar cliente e FK rejeita vínculo externo no PostgreSQL @spec:AC-098")]
    [InlineData("Pending")]
    [InlineData("Sent")]
    public async Task UpgradePreservesHistoryAndProtectsCustomerLinks(string status)
    {
        await using var fixture = await Database.Create(migrationTarget: "20261012041401_EnforceTenantRowLevelSecurity");
        await Execute(fixture.Admin, $"UPDATE \"NotificationMessages\" SET \"Status\"='{status}', \"RetryCount\"=2, \"LastError\"='historical error', \"SentAt\"=now(), \"NextRetryAt\"=now()+interval '4 minutes'");
        var before = (string)(await Scalar(fixture.Admin, "SELECT jsonb_agg(to_jsonb(n) ORDER BY \"Id\")::text FROM \"NotificationMessages\" n"))!;
        var options = new DbContextOptionsBuilder<AgendamentoDbContext>().UseNpgsql(fixture.MigrationConnection).Options;
        await using (var db = new AgendamentoDbContext(options, new Tenant(Guid.Empty)))
        {
            await db.Database.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        }
        var after = (string)(await Scalar(fixture.Admin, "SELECT jsonb_agg(to_jsonb(n)-'Audience'-'CustomerId'-'ReceivableTitleId'-'ReminderDate' ORDER BY \"Id\")::text FROM \"NotificationMessages\" n"))!;
        Assert.Equal(before, after);
        Assert.Equal(2L, await Scalar(fixture.Admin, "SELECT count(*) FROM \"NotificationMessages\" WHERE \"CustomerId\" IS NULL AND \"Audience\"='LegacyUnlinked'"));
        var customer = Agendamento.Api.Domain.Customers.Customer.Create(fixture.A, "Cliente do histórico", "123", null, null, null);
        await using (var seed = new AgendamentoDbContext(options, new Tenant(fixture.A)))
        {
            seed.Customers.Add(customer); await seed.SaveChangesAsync();
        }
        var ownCustomer = customer.Id;
        var foreignCustomer = (Guid)(await Scalar(fixture.Admin, "SELECT \"Id\" FROM \"Customers\" WHERE tenant_id=@id::uuid", fixture.B.ToString()))!;
        await using var connection = new NpgsqlConnection(fixture.RestrictedConnection); await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();
        await Execute(connection, "SELECT set_config('app.tenant_id', @id, true)", fixture.A.ToString());
        Assert.Equal(1L, await Scalar(connection, "SELECT count(*) FROM \"NotificationMessages\""));
        Assert.Equal(1, await Execute(connection, "UPDATE \"NotificationMessages\" SET \"Audience\"='Customer', \"CustomerId\"=@id::uuid", ownCustomer.ToString()));
        await Execute(connection, "SAVEPOINT invalid_link");
        var foreign = await Assert.ThrowsAsync<PostgresException>(() => Execute(connection,
            "UPDATE \"NotificationMessages\" SET \"CustomerId\"=@id::uuid", foreignCustomer.ToString()));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, foreign.SqlState);
        await Execute(connection, "ROLLBACK TO SAVEPOINT invalid_link");
        var missing = await Assert.ThrowsAsync<PostgresException>(() => Execute(connection,
            "UPDATE \"NotificationMessages\" SET \"CustomerId\"=NULL"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, missing.SqlState);
        await Execute(connection, "ROLLBACK TO SAVEPOINT invalid_link");
        var deletion = await Assert.ThrowsAsync<PostgresException>(() => Execute(connection,
            "DELETE FROM \"Customers\" WHERE \"Id\"=@id::uuid", ownCustomer.ToString()));
        Assert.Equal(PostgresErrorCodes.RestrictViolation, deletion.SqlState);
        Assert.Equal("FK_NotificationMessages_Customers_tenant_id_CustomerId", deletion.ConstraintName);
        await Execute(connection, "ROLLBACK TO SAVEPOINT invalid_link");
        Assert.Equal(0, await Execute(connection, "UPDATE \"NotificationMessages\" SET \"Content\"='foreign write' WHERE tenant_id=@id::uuid", fixture.B.ToString()));
        await tx.CommitAsync();
        Assert.Equal(1L, await Scalar(fixture.Admin, "SELECT count(*) FROM \"NotificationMessages\" WHERE \"CustomerId\"=@id::uuid AND tenant_id='" + fixture.A + "'", ownCustomer.ToString()));
        Assert.Equal(1L, await Scalar(fixture.Admin, "SELECT count(*) FROM \"NotificationMessages\" WHERE \"Audience\"='LegacyUnlinked' AND tenant_id=@id::uuid", fixture.B.ToString()));
        using var factory = new TeamFinancialTestFactory(fixture.RestrictedConnection, fixture.AdministrativeConnection);
        var (client, _, _) = await factory.Member("owner_admin", [], fixture.A);
        var queued = await client.PostAsJsonAsync("/api/v1/notifications/send-manual",
            new SendManualNotificationRequest("123", "Email", "Nova mensagem", ownCustomer));
        Assert.Equal(HttpStatusCode.OK, queued.StatusCode);
        var notificationId = (await queued.Content.ReadFromJsonAsync<NotificationQueuedResponse>())!.NotificationId;
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<INotificationService>().ProcessPendingAsync();
        var history = await client.GetFromJsonAsync<List<NotificationHistoryResponse>>($"/api/v1/notifications/?customerId={ownCustomer}");
        var delivered = Assert.Single(history!, n => n.Id == notificationId);
        Assert.Equal(ownCustomer, delivered.CustomerId); Assert.Equal("Customer", delivered.Audience);
        Assert.Equal("Sent", delivered.Status); Assert.NotNull(delivered.SentAt);
    }
}
