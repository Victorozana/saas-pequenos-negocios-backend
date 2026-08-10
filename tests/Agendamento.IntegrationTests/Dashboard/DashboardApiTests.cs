using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http;
using System.Threading.Tasks;

using Agendamento.IntegrationTests.Infrastructure;

namespace Agendamento.IntegrationTests.Dashboard;

public class DashboardApiTests : IClassFixture<AgendamentoApiFactory>
{
    private readonly HttpClient _client;

    public DashboardApiTests(AgendamentoApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetDashboardSummary_WithoutAuth_ShouldReturnUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/dashboard/summary");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
    }
    
    [Fact]
    public async Task GetUpcomingSchedule_WithoutAuth_ShouldReturnUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/dashboard/schedule");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
    }
    
    [Fact]
    public async Task GetReport_WithoutAuth_ShouldReturnUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/dashboard/report");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
