using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

using Agendamento.IntegrationTests.Infrastructure;

namespace Agendamento.IntegrationTests.OpenApi;

public class SwaggerUiTests : IClassFixture<AgendamentoApiFactory>
{
    private readonly AgendamentoApiFactory _factory;

    public SwaggerUiTests(AgendamentoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "Swagger UI permite explorar a versão vigente em ambiente Test @spec:AC-040")]
    public async Task Given_TestEnvironment_When_SwaggerUiIsAccessed_Then_ReturnsOk()
    {
        // Arrange
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Environment", "Test");
            builder.UseSetting("ConnectionStrings:PostgreSql", "Host=localhost;Database=dummy;Username=dummy;Password=dummy");
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/swagger/index.html");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("swagger-ui", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Documento canônico em /openapi/v1.json é exposto @spec:AC-036")]
    public async Task Given_TestEnvironment_When_OpenApiDocumentIsAccessed_Then_ReturnsOk()
    {
        // Arrange
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:PostgreSql", "Host=localhost;Database=dummy;Username=dummy;Password=dummy");
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/openapi/v1.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("openapi", content);
        Assert.Contains("Agendamento API v1", content);
    }
}
