using System.Linq;
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
}
