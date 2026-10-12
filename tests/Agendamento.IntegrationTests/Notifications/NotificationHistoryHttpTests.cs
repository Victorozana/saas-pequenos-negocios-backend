using System.Net;
using System.Net.Http.Json;
using Agendamento.Api.Domain.Customers;
using Agendamento.Api.Domain.Notifications;
using Agendamento.Api.Features.Notifications;
using Agendamento.IntegrationTests.Features;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Agendamento.IntegrationTests.Notifications;

public sealed class NotificationHistoryHttpTests : IClassFixture<TeamFinancialTestFactory>
{
    private readonly TeamFinancialTestFactory factory;
    public NotificationHistoryHttpTests(TeamFinancialTestFactory factory) => this.factory = factory;

    [Fact(DisplayName = "Histórico HTTP vincula cliente e isola empresas e mensagens avulsas @spec:AC-098 @spec:AC-097")]
    public async Task ManualHistoryHasExplicitCustomerAndAudience()
    {
        var (client, _, tenant) = await factory.Member("owner_admin", []);
        var (other, _, otherTenant) = await factory.Member("owner_admin", []);
        var first = Customer.Create(tenant, "Primeiro", "123", null, null, null);
        var second = Customer.Create(tenant, "Segundo", "123", null, null, null);
        var foreign = Customer.Create(otherTenant, "Externo", "123", null, null, null);
        using (var db = factory.Db(tenant)) { db.Customers.AddRange(first, second); await db.SaveChangesAsync(); }
        using (var db = factory.Db(otherTenant)) { db.Customers.Add(foreign); await db.SaveChangesAsync(); }
        foreach (var customer in new[] { first, second })
        {
            var response = await client.PostAsJsonAsync("/api/v1/notifications/send-manual",
                new SendManualNotificationRequest("123", "WhatsApp", "Mensagem", customer.Id));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/notifications/send-manual",
            new SendManualNotificationRequest("123", "Email", "Avulsa"))).StatusCode);
        foreach (var id in new[] { foreign.Id, Guid.NewGuid(), Guid.Empty })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/notifications/send-manual",
                new SendManualNotificationRequest("123", "Email", "Inválida", id))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/notifications/send-manual",
            new SendManualNotificationRequest("123", "999", "Inválida", first.Id))).StatusCode);
        var all = await client.GetFromJsonAsync<List<NotificationHistoryResponse>>("/api/v1/notifications/");
        Assert.Equal(3, all!.Count);
        Assert.Equal(2, all.Count(n => n.Audience == "Customer" && n.CustomerId.HasValue));
        var standalone = Assert.Single(all, n => n.Audience == "Standalone"); Assert.Null(standalone.CustomerId);
        Assert.All(all, n => Assert.Equal("Pending", n.Status));
        var filtered = await client.GetFromJsonAsync<List<NotificationHistoryResponse>>($"/api/v1/notifications/?customerId={first.Id}");
        Assert.Equal(first.Id, Assert.Single(filtered!).CustomerId);
        Assert.Empty((await other.GetFromJsonAsync<List<NotificationHistoryResponse>>("/api/v1/notifications/"))!);
        Assert.Empty((await other.GetFromJsonAsync<List<NotificationHistoryResponse>>($"/api/v1/notifications/?customerId={first.Id}"))!);
        // Persist delivery using the same audit row; customer filtering must still work after sending.
        using (var db = factory.Db(tenant))
        {
            var message = await db.NotificationMessages.SingleAsync(n => n.CustomerId == first.Id);
            message.MarkAsSent(); await db.SaveChangesAsync();
            Assert.Equal(3, await db.NotificationMessages.CountAsync());
            Assert.All(await db.NotificationMessages.ToListAsync(), n => Assert.Equal(tenant, n.TenantId));
        }
        var sent = Assert.Single((await client.GetFromJsonAsync<List<NotificationHistoryResponse>>($"/api/v1/notifications/?customerId={first.Id}"))!);
        Assert.Equal("Sent", sent.Status); Assert.NotNull(sent.SentAt); Assert.Equal(first.Id, sent.CustomerId);
    }
}

