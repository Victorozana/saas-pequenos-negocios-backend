using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Agendamento.IntegrationTests.Infrastructure;
using Agendamento.Application.Identity.CreateSession;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.Api.Domain.Financial;
using System;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Agendamento.IntegrationTests.Dashboard;

public class DashboardApiTests : IClassFixture<AgendamentoApiFactory>
{
    private readonly AgendamentoApiFactory _factory;
    private readonly HttpClient _client;

    public DashboardApiTests(AgendamentoApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetDashboardSummary_WithoutAuth_ShouldReturnUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/dashboard/summary");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }
    
    [Fact]
    public async Task GetUpcomingSchedule_WithoutAuth_ShouldReturnUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/dashboard/schedule");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }
    
    [Fact]
    public async Task GetReport_WithoutAuth_ShouldReturnUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/dashboard/report");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }
    
    [Fact(DisplayName = "Isolamento estrito de dados por tenant no dashboard @spec:AC-065")]
    public async Task DashboardEndpoints_ShouldReturnOnlyTenantData_AC065()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgendamentoDbContext>();
            var titleA = ReceivableTitle.Create(tenantA, "Tenant A", 100m, DateTime.UtcNow);
            titleA.RegisterPayment(100m, PaymentMethod.Pix, DateTime.UtcNow);
            
            var titleB = ReceivableTitle.Create(tenantB, "Tenant B", 200m, DateTime.UtcNow);
            titleB.RegisterPayment(200m, PaymentMethod.Pix, DateTime.UtcNow);
            
            db.ReceivableTitles.Add(titleA);
            db.ReceivableTitles.Add(titleB);
            await db.SaveChangesAsync();
        }

        var issuer = _factory.Services.GetRequiredService<Agendamento.Application.Identity.ISessionIssuer>();
        var principal = new SessionPrincipal(userId, tenantA, "Admin");
        var session = issuer.Issue(principal);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/dashboard/summary");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.EnsureSuccessStatusCode();
        var contentStr = await response.Content.ReadAsStringAsync();
        var content = JsonDocument.Parse(contentStr).RootElement;
        
        var totalReceivedAmount = content.GetProperty("totalReceivedAmount").GetDecimal();
        Assert.Equal(100m, totalReceivedAmount); // Should only see Tenant A's 100m, not B's 200m
    }
}
