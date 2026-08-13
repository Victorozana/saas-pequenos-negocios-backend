using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Microsoft.AspNetCore.Http.Metadata;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;

namespace Agendamento.ArchitectureTests.OpenApi;

public class EndpointMetadataTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public EndpointMetadataTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => 
        {
            builder.UseSetting("Environment", "Test");
            builder.UseSetting("ConnectionStrings:PostgreSql", "Host=localhost;Database=dummy;Username=dummy;Password=dummy");
        });
    }

    [Fact(DisplayName = "Toda operação possui identificador estável, tags adequadas @spec:AC-037")]
    public void Endpoints_Should_Have_OperationId_And_Tags()
    {
        var endpointDataSource = _factory.Services.GetRequiredService<EndpointDataSource>();
        var endpoints = endpointDataSource.Endpoints.OfType<RouteEndpoint>().ToList();

        Assert.NotEmpty(endpoints);

        foreach (var endpoint in endpoints)
        {
            var routePattern = endpoint.RoutePattern.RawText;
            if (routePattern == null || routePattern.StartsWith("/swagger") || routePattern.StartsWith("/openapi"))
            {
                continue;
            }

            var endpointNameMetadata = endpoint.Metadata.GetMetadata<IEndpointNameMetadata>();
            Assert.True(endpointNameMetadata != null, $"Endpoint {routePattern} doesn't have a name/operationId.");
            Assert.False(string.IsNullOrEmpty(endpointNameMetadata.EndpointName));

            var tagsMetadata = endpoint.Metadata.GetMetadata<ITagsMetadata>();
            Assert.True(tagsMetadata != null && tagsMetadata.Tags.Count > 0, $"Endpoint {routePattern} doesn't have tags.");
        }
    }

    [Fact(DisplayName = "@spec:AC-100 contrato documenta o header de verificação somente para desenvolvimento")]
    public async Task Tenant_registration_contract_documents_development_verification_header()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var tenantRegistration = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/v1/tenants")
            .GetProperty("post")
            .GetProperty("responses")
            .GetProperty("201");

        var header = tenantRegistration
            .GetProperty("headers")
            .GetProperty("X-Development-Verification-Token");

        Assert.Equal("string", header.GetProperty("schema").GetProperty("type").GetString());
        Assert.Contains("Development", header.GetProperty("description").GetString());

        var responseProperties = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("RegisterTenantResponse")
            .GetProperty("properties");

        Assert.False(responseProperties.TryGetProperty("verificationToken", out _));
    }

    [Fact(DisplayName = "Contrato de autenticação documenta falhas seguras e perfil protegido @spec:AC-013")]
    public async Task Authentication_contract_documents_failure_responses()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        var sessionResponses = paths.GetProperty("/api/v1/auth/sessions").GetProperty("post").GetProperty("responses");
        var profileResponses = paths.GetProperty("/api/v1/users/me").GetProperty("get").GetProperty("responses");

        Assert.All(new[] { "200", "400", "401", "403", "503" }, status => Assert.True(sessionResponses.TryGetProperty(status, out _), $"Session response {status} is missing."));
        Assert.All(new[] { "200", "401", "403", "404" }, status => Assert.True(profileResponses.TryGetProperty(status, out _), $"Profile response {status} is missing."));
    }

    [Fact(DisplayName = "Contrato cadastral tipa sucesso e falhas seguras @spec:AC-017")]
    public async Task Company_registry_contract_documents_typed_responses()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var operation = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/v1/company-registry/{cnpj}")
            .GetProperty("get");
        var responses = operation.GetProperty("responses");

        Assert.All(new[] { "200", "400", "404", "422", "429", "503" }, status =>
            Assert.True(responses.TryGetProperty(status, out _), $"Company registry response {status} is missing."));

        var successSchemaReference = responses
            .GetProperty("200")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema")
            .GetProperty("$ref")
            .GetString();
        Assert.Equal("#/components/schemas/CompanyRegistryResult", successSchemaReference);

        var properties = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("CompanyRegistryResult")
            .GetProperty("properties");
        Assert.All(new[] { "cnpj", "corporateName", "tradeName", "legalNature", "cnaes", "status", "address" }, property =>
            Assert.True(properties.TryGetProperty(property, out _), $"Company registry property {property} is missing."));
    }
}
