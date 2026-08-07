using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Agendamento.ArchitectureTests.OpenApi;

public class OpenApiCompletenessTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OpenApiCompletenessTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => 
        {
            builder.UseSetting("Environment", "Test");
            builder.UseSetting("ConnectionStrings:PostgreSql", "Host=localhost;Database=dummy;Username=dummy;Password=dummy");
        });
    }

    [Fact(DisplayName = "Todos os endpoints da aplicação devem estar documentados no OpenAPI")]
    public async Task All_Endpoints_Should_Be_Documented()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var paths = doc.RootElement.GetProperty("paths");

        var endpointDataSource = _factory.Services.GetRequiredService<EndpointDataSource>();
        var endpoints = endpointDataSource.Endpoints.OfType<RouteEndpoint>().ToList();

        foreach (var endpoint in endpoints)
        {
            var routePattern = endpoint.RoutePattern.RawText;
            if (routePattern == null || routePattern.StartsWith("/swagger") || routePattern.StartsWith("/openapi"))
            {
                continue;
            }

            var excludeMetadata = endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.IExcludeFromDescriptionMetadata>();
            if (excludeMetadata != null && excludeMetadata.ExcludeFromDescription)
            {
                continue;
            }

            var openApiPath = routePattern.StartsWith("/") ? routePattern : "/" + routePattern;
            if (openApiPath.EndsWith("/") && openApiPath.Length > 1)
            {
                openApiPath = openApiPath.TrimEnd('/');
            }

            // Route parameters in OpenAPI use {param}
            // RouteEndpoint routePattern can have constraints e.g. {id:guid}
            // We should strip constraints for OpenAPI comparison.
            openApiPath = System.Text.RegularExpressions.Regex.Replace(openApiPath, @"\{([^:}]+):[^}]+\}", "{$1}");

            Assert.True(paths.TryGetProperty(openApiPath, out var pathItem), $"Path '{openApiPath}' not found in OpenAPI doc");

            // Also check HTTP method
            var httpMethodMetadata = endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>();
            if (httpMethodMetadata != null && httpMethodMetadata.HttpMethods.Count > 0)
            {
                foreach (var method in httpMethodMetadata.HttpMethods)
                {
                    var openApiMethod = method.ToLowerInvariant();
                    Assert.True(pathItem.TryGetProperty(openApiMethod, out _), $"Method '{method}' for '{openApiPath}' not found in OpenAPI doc");
                }
            }
        }
    }
}
