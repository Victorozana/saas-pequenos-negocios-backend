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

    [Fact]
    public async Task GetTenantProfile_WithoutToken_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/tenants/me");
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Unauthorized == response.StatusCode, content);
    }

    [Fact]
    public async Task GetTenantProfile_WithTenantInSession_IgnoresExternalTenantIdHeader()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var issuer = _factory.Services.GetRequiredService<Agendamento.Application.Identity.ISessionIssuer>();
        var principal = new Agendamento.Application.Identity.CreateSession.SessionPrincipal(userId, tenantA, "Admin");
        var session = issuer.Issue(principal);
        var token = session.AccessToken;

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/tenants/me");
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
}
