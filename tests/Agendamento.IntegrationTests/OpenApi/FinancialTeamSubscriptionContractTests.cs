using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Agendamento.Domain.Subscriptions;
using Agendamento.IntegrationTests.Features;
using Xunit;

namespace Agendamento.IntegrationTests.OpenApi;

public sealed class FinancialTeamSubscriptionContractTests : IClassFixture<TeamFinancialTestFactory>
{
    private readonly TeamFinancialTestFactory _factory;
    public FinancialTeamSubscriptionContractTests(TeamFinancialTestFactory factory) => _factory = factory;

    [Fact(DisplayName = "Contrato financeiro/equipe preserva 200, 204 e 404 vazios e erros seguros @spec:AC-037")]
    public async Task FinancialAndTeamResponsesMatchContract()
    {
        var (client, adminId, tenant) = await _factory.Member("owner_admin", []);
        var (_, foreignUser, _) = await _factory.Member("owner_admin", []);
        var (_, employee, _) = await _factory.Member("Employee", [], tenant);
        var document = await Contract(client);
        await Empty(client, await client.PutAsJsonAsync($"/api/v1/team-members/{foreignUser}",
            new { role = "Employee", permissions = Array.Empty<string>() }), document,
            "/api/v1/team-members/{userId}", "put", 404);
        await Empty(client, await client.PatchAsJsonAsync($"/api/v1/team-members/{employee}/status",
            new { isActive = false }), document, "/api/v1/team-members/{userId}/status", "patch", 204);
        await Problem(await client.PatchAsJsonAsync($"/api/v1/team-members/{adminId}/status",
            new { isActive = false }), document, "/api/v1/team-members/{userId}/status", "patch", 409);
        await Problem(await client.PostAsJsonAsync("/api/v1/team-members/invitations",
            new { email = "invalid", role = "Employee" }), document, "/api/v1/team-members/invitations", "post", 400);
        var created = await client.PostAsJsonAsync("/api/v1/financial/payables",
            new { supplierName = "Fornecedor", description = "Despesa", amount = 100, dueDate = DateTime.UtcNow });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("application/json", created.Content.Headers.ContentType?.MediaType);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.EndsWith(id.ToString(), created.Headers.Location?.ToString());
        var payment = new { amount = 40, method = 0, paymentDate = DateTime.UtcNow };
        await Empty(client, await client.PostAsJsonAsync($"/api/v1/financial/payables/{id}/pay", payment),
            document, "/api/v1/financial/payables/{id}/pay", "post", 200);
        var (other, _, _) = await _factory.Member("owner_admin", []);
        await Empty(other, await other.PostAsJsonAsync($"/api/v1/financial/payables/{id}/pay", payment),
            document, "/api/v1/financial/payables/{id}/pay", "post", 404);
        await Problem(await client.PostAsJsonAsync($"/api/v1/financial/payables/{id}/pay",
            new { amount = 61, method = 0, paymentDate = DateTime.UtcNow }), document,
            "/api/v1/financial/payables/{id}/pay", "post", 400);
        await Problem(await client.GetAsync("/api/v1/financial/statement?startDate=2026-10-12&endDate=2026-10-11"),
            document, "/api/v1/financial/statement", "get", 400);
        var receivable = Agendamento.Api.Domain.Financial.ReceivableTitle.Create(tenant, "Recebível", 100, DateTime.UtcNow);
        using (var db = _factory.Db(tenant))
        {
            db.ReceivableTitles.Add(receivable);
            await db.SaveChangesAsync();
        }
        await Empty(client, await client.PostAsJsonAsync($"/api/v1/financial/receivables/{receivable.Id}/pay", payment),
            document, "/api/v1/financial/receivables/{id}/pay", "post", 200);
        await Empty(other, await other.PostAsJsonAsync($"/api/v1/financial/receivables/{receivable.Id}/pay", payment),
            document, "/api/v1/financial/receivables/{id}/pay", "post", 404);
        await Problem(await client.PostAsJsonAsync($"/api/v1/financial/receivables/{receivable.Id}/pay",
            new { amount = 61, method = 0, paymentDate = DateTime.UtcNow }), document,
            "/api/v1/financial/receivables/{id}/pay", "post", 400);
    }

    [Fact(DisplayName = "Contrato assinatura cobre ausência isolada, troca vazia e webhook inválido @spec:AC-037")]
    public async Task SubscriptionsAndWebhookMatchContract()
    {
        var (client, _, tenant) = await _factory.Member("owner_admin", []);
        var (foreign, _, foreignTenant) = await _factory.Member("owner_admin", []);
        var document = await Contract(client);
        await Problem(await client.GetAsync("/api/v1/subscriptions/current"), document,
            "/api/v1/subscriptions/current", "get", 404);
        var plan = SaasPlan.Create("Contrato", "Teste", 50, "Monthly", new PlanLimits(5, 100, 10));
        using (var db = _factory.Db(tenant))
        {
            db.SaasPlans.Add(plan);
            db.TenantSubscriptions.Add(TenantSubscription.CreateTrial(tenant, plan.Id));
            await db.SaveChangesAsync();
        }
        var current = await client.GetAsync("/api/v1/subscriptions/current");
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        Assert.Equal("application/json", current.Content.Headers.ContentType?.MediaType);
        Assert.Equal(plan.Id, (await current.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("planId").GetGuid());
        await Problem(await foreign.GetAsync("/api/v1/subscriptions/current"), document,
            "/api/v1/subscriptions/current", "get", 404);
        await Problem(await client.PostAsJsonAsync("/api/v1/subscriptions/change-plan", new { newPlanId = Guid.NewGuid() }),
            document, "/api/v1/subscriptions/change-plan", "post", 404);
        await Empty(client, await client.PostAsJsonAsync("/api/v1/subscriptions/change-plan", new { newPlanId = plan.Id }),
            document, "/api/v1/subscriptions/change-plan", "post", 204);
        using var anonymous = _factory.CreateClient();
        foreach (var payload in new object[] { new { }, new { id = "evt", type = "invoice.paid", data = new { } } })
            await Problem(await anonymous.PostAsJsonAsync("/api/v1/webhooks/billing", payload), document,
                "/api/v1/webhooks/billing", "post", 400);
        await Empty(anonymous, await anonymous.PostAsJsonAsync("/api/v1/webhooks/billing",
            new { id = Guid.NewGuid().ToString(), type = "invoice.paid", data = new { @object = new { id = "unknown" } } }),
            document, "/api/v1/webhooks/billing", "post", 200);
    }

    private static async Task<JsonElement> Contract(HttpClient client) =>
        await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");

    private static JsonElement Response(JsonElement contract, string path, string method, int status) =>
        contract.GetProperty("paths").GetProperty(path).GetProperty(method).GetProperty("responses").GetProperty(status.ToString());

    private static async Task Empty(HttpClient _, HttpResponseMessage response, JsonElement contract, string path, string method, int status)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        Assert.Null(response.Content.Headers.ContentType);
        var metadata = Response(contract, path, method, status);
        Assert.False(metadata.TryGetProperty("content", out var content) && content.EnumerateObject().Any());
    }

    private static async Task Problem(HttpResponseMessage response, JsonElement contract, string path, string method, int status)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(status, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("title").GetString()));
        Assert.True(Response(contract, path, method, status).GetProperty("content").GetProperty("application/problem+json").TryGetProperty("schema", out _));
        var raw = body.GetRawText();
        Assert.DoesNotContain("Exception", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
    }
}
