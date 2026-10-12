using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Agendamento.Api.Application.Customers.UpdateCustomer;
using Agendamento.Api.Domain.Customers;
using Agendamento.IntegrationTests.Features;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Agendamento.IntegrationTests.Notifications;

public sealed class AutomaticReminderPreferenceHttpTests : IClassFixture<TeamFinancialTestFactory>
{
    private readonly TeamFinancialTestFactory factory;
    public AutomaticReminderPreferenceHttpTests(TeamFinancialTestFactory factory) => this.factory = factory;

    [Fact(DisplayName = "Criação HTTP aceita desativação explícita e mantém padrão habilitado para contratos antigos @spec:AC-207")]
    public async Task CreationExposesDefaultAndExplicitPreference()
    {
        var (client, _, _) = await factory.Member("owner_admin", []);
        foreach (var enabled in new[] { true, false })
        {
            object request = enabled ? new { name = "Padrão fictício", phone = "123" } :
                new { name = "Desativado fictício", phone = "123", automaticRemindersEnabled = false };
            var response = await client.PostAsJsonAsync("/api/v1/customers", request);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            using var created = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var id = created.RootElement.GetProperty("id").GetGuid();
            var detail = await client.GetAsync($"/api/v1/customers/{id}"); Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            using var customer = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
            Assert.Equal(enabled, customer.RootElement.GetProperty("automaticRemindersEnabled").GetBoolean());
        }
    }

    [Fact(DisplayName = "Preferência persiste na edição, detalhe e lista com CPF mascarado e isolamento HTTP @spec:AC-207 @spec:AC-038")]
    public async Task EditPreferencePreservesDocumentAndOtherTenants()
    {
        var (client, _, tenant) = await factory.Member("owner_admin", []);
        var (other, _, otherTenant) = await factory.Member("owner_admin", []);
        var person = Customer.Create(tenant, "Pessoa fictícia", "123", null, CustomerDocument.Create("CPF", "12345678909"), null);
        var foreign = Customer.Create(otherTenant, "Outra pessoa", "123", null, null, null);
        using (var db = factory.Db(tenant)) { db.Customers.Add(person); await db.SaveChangesAsync(); }
        using (var db = factory.Db(otherTenant)) { db.Customers.Add(foreign); await db.SaveChangesAsync(); }
        var command = new UpdateCustomerCommand(person.Id, "Nome editado", person.Phone, null, null, null, null, null, null, null, null, null, null, true, false);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/v1/customers/{person.Id}", command)).StatusCode);
        foreach (var path in new[] { "/api/v1/customers", $"/api/v1/customers/{person.Id}" })
        {
            var response = await client.GetAsync(path); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("12345678909", json);
            using var parsed = JsonDocument.Parse(json);
            var item = path.EndsWith(person.Id.ToString()) ? parsed.RootElement : parsed.RootElement.GetProperty("items").EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == person.Id);
            Assert.False(item.GetProperty("automaticRemindersEnabled").GetBoolean());
            Assert.Equal("***.***.***-09", item.GetProperty("documentValue").GetString());
            Assert.Equal("Nome editado", item.GetProperty("name").GetString());
        }
        // Older clients omit the optional preference; unrelated edits must preserve the opt-out.
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/v1/customers/{person.Id}", command with { Name = "Segunda edição", AutomaticRemindersEnabled = null })).StatusCode);
        using (var db = factory.Db(tenant))
        {
            var saved = await db.Customers.SingleAsync(c => c.Id == person.Id);
            Assert.False(saved.AutomaticRemindersEnabled); Assert.Equal("12345678909", saved.Document!.Value);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/v1/customers/{person.Id}", command with { DocumentType = "CPF", DocumentValue = "***.***.***-09" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/v1/customers/{person.Id}", command with { AutomaticRemindersEnabled = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/customers/{person.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/v1/customers/{foreign.Id}", command with { CustomerId = foreign.Id })).StatusCode);
        using (var db = factory.Db(otherTenant)) Assert.True((await db.Customers.SingleAsync(c => c.Id == foreign.Id)).AutomaticRemindersEnabled);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/v1/customers/{person.Id}", command with { AutomaticRemindersEnabled = true })).StatusCode);
        using (var db = factory.Db(tenant)) Assert.True((await db.Customers.SingleAsync(c => c.Id == person.Id)).AutomaticRemindersEnabled);
    }
}
