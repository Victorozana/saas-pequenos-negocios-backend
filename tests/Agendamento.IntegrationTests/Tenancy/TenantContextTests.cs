using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Agendamento.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Agendamento.Application.Identity.CreateSession;
using Agendamento.Domain.Identity;
using Xunit;

namespace Agendamento.IntegrationTests.Tenancy;

public class TenantContextTests : IClassFixture<AgendamentoApiFactory>
{
    private readonly AgendamentoApiFactory _factory;
    private readonly HttpClient _client;

    public TenantContextTests(AgendamentoApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact(DisplayName = "Requisição sem sessão não acessa perfil privado @spec:AC-028")]
    public async Task GetTenantProfile_WithoutToken_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/tenants/me");
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Unauthorized == response.StatusCode, content);
    }

    [Theory(DisplayName = "Tenant externo em header ou query não substitui a sessão @spec:AC-029 @principle:P-003")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetTenantProfile_WithTenantInSession_IgnoresExternalTenantIdHeader(bool useQuery)
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userId = await _factory.CreateActiveMemberAsync(tenantA);

        var issuer = _factory.Services.GetRequiredService<Agendamento.Application.Identity.ISessionIssuer>();
        var principal = new Agendamento.Application.Identity.CreateSession.SessionPrincipal(userId, tenantA, "Admin");
        var session = issuer.Issue(principal);
        var token = session.AccessToken;

        var request = new HttpRequestMessage(HttpMethod.Get, useQuery
            ? $"/api/v1/tenants/me?tenant_id={tenantB}&tenantId={tenantB}"
            : "/api/v1/tenants/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("tenant_id", tenantB.ToString()); // Attempt to override

        // Act
        var response = await _client.SendAsync(request);
        var contentStr = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, contentStr);
        var content = System.Text.Json.JsonDocument.Parse(contentStr).RootElement;
        var resolvedTenantId = content.GetProperty("tenantId").GetGuid();

        Assert.Equal(tenantA, resolvedTenantId); // Tenant A must be preserved
        Assert.NotEqual(tenantB, resolvedTenantId);
    }

    [Fact(DisplayName = "Tenant externo no body não muda a empresa do cliente criado @spec:AC-029 @principle:P-003")]
    public async Task CreateCustomer_IgnoresExternalTenantIdBody()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userId = await _factory.CreateActiveMemberAsync(tenantA);
        var issuer = _factory.Services.GetRequiredService<Agendamento.Application.Identity.ISessionIssuer>();
        var session = issuer.Issue(new SessionPrincipal(userId, tenantA, "Admin"));
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var created = await client.PostAsJsonAsync("/api/v1/customers", new
        {
            name = "Cliente de isolamento", phone = "11999999999", tenantId = tenantB, tenant_id = tenantB
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Agendamento.Api.Infrastructure.Persistence.AgendamentoDbContext>();
        var customer = Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.IgnoreQueryFilters(db.Customers)
            .Single(c => c.Id == id);
        Assert.Equal(tenantA, customer.TenantId);
        Assert.NotEqual(tenantB, customer.TenantId);
    }
}
