using Microsoft.Extensions.Configuration;
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
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string,string?> { ["Database:RequirePostgreSql"] = "false" }));
        });
    }

    [Fact(DisplayName = "Todos os endpoints da aplicação devem estar documentados no OpenAPI @spec:AC-036")]
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

    [Fact(DisplayName = "Saídas JSON antes não tipadas possuem schemas verificáveis @spec:AC-038 @principle:P-005")]
    public async Task Json_outputs_have_inspectable_schemas()
    {
        using var client = _factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");
        foreach (var (path, method, status) in new[]
        {
            ("/api/v1/tenants/me", "get", "200"), ("/api/v1/services", "post", "201"),
            ("/api/v1/quotations", "post", "201"), ("/api/v1/quotations/{id}/convert", "post", "200"),
            ("/api/v1/appointments", "post", "201"), ("/api/v1/notifications", "get", "200"),
            ("/api/v1/customers", "post", "201"), ("/api/v1/customers", "get", "200"),
            ("/api/v1/customers/{id}", "get", "200"),
            ("/api/v1/notifications/send-manual", "post", "200"),
            ("/api/v1/notifications/schedule-reminder", "post", "200"),
            ("/api/v1/notifications/send-quotation-notification", "post", "200"),
            ("/api/v1/subscriptions/current", "get", "200"), ("/api/v1/subscriptions/plans", "get", "200")
        })
        {
            Assert.True(paths.GetProperty(path).GetProperty(method).GetProperty("responses").GetProperty(status)
                .GetProperty("content").GetProperty("application/json").TryGetProperty("schema", out _), $"Missing output schema: {method} {path}");
        }
    }
}
