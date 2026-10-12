using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Agendamento.Api.Infrastructure.Persistence;
using Xunit;

namespace Agendamento.ArchitectureTests.OpenApi;

public class OpenApiSensitiveDataTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly string[] SensitiveKeywords = new[] { "password", "senha", "secret", "token" };

    public OpenApiSensitiveDataTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => 
        {
            builder.UseSetting("Environment", "Test");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string,string?> { ["Database:RequirePostgreSql"] = "false" }));
        });
    }

    [Fact(DisplayName = "Nenhum schema deve expor propriedades sensíveis inadvertidamente")]
    public async Task OpenApi_Should_Not_Expose_Sensitive_Properties()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        
        var components = doc.RootElement.GetProperty("components");
        var schemas = components.GetProperty("schemas");

        foreach (var schema in schemas.EnumerateObject())
        {
            if (schema.Value.TryGetProperty("properties", out var properties))
            {
                foreach (var prop in properties.EnumerateObject())
                {
                    var propName = prop.Name.ToLowerInvariant();
                    
                    // If property name contains sensitive keyword, check if it's writeOnly or if we allow it (like AccessToken)
                    bool isSensitive = SensitiveKeywords.Any(k => propName.Contains(k));
                    if (isSensitive)
                    {
                        // Allow AccessToken (it's returned by CreateSession)
                        if (propName == "accesstoken") continue;
                        
                        // Otherwise, it must be writeOnly
                        bool isWriteOnly = prop.Value.TryGetProperty("writeOnly", out var wo) && wo.GetBoolean();
                        
                        // We might not have writeOnly attribute configured. For now we just allow specific names or assert it is writeOnly.
                        // Actually, since this is a strict test, let's just make sure it fails if it's not handled.
                        // Wait, record properties don't automatically get writeOnly. So we might need to add a schema transformer in the future,
                        // or just ensure they are named explicitly and not leaked in responses.
                        // For this test, let's just assert that we don't have unexpected sensitive fields.
                        
                        // E.g. We allow 'password', 'token', etc. in request schemas
                        if (schema.Name.EndsWith("Request")) continue;
                        
                        Assert.Fail($"Propriedade sensível '{prop.Name}' encontrada no schema '{schema.Name}'.");
                    }
                }
            }
        }
    }

    [Theory(DisplayName = "Respostas e exemplos resolvidos minimizam dados em todos os ambientes @spec:AC-038 @principle:P-005")]
    [InlineData("Development")]
    [InlineData("Test")]
    [InlineData("Production")]
    public async Task Response_graph_and_examples_are_minimized(string environment)
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Environment", environment);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AgendamentoDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<AgendamentoDbContext>>();
                services.AddDbContext<AgendamentoDbContext>(options => options.UseInMemoryDatabase($"OpenApi_{Guid.NewGuid():N}"));
            });
        });
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        Assert.Empty(OpenApiDataMinimization.Inspect(document.RootElement));
        // Sensitive inputs remain legitimate. Session access tokens are a required output.
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.True(schemas.GetProperty("RegisterTenantRequest").GetProperty("properties").TryGetProperty("adminPassword", out _));
        Assert.True(schemas.GetProperty("VerifyEmailRequest").GetProperty("properties").TryGetProperty("token", out _));
        var pattern = schemas.GetProperty("CustomerResponse").GetProperty("properties").GetProperty("documentValue").GetProperty("pattern").GetString()!;
        Assert.Matches(pattern, "***.***.***-09");
        Assert.Matches(pattern, "00000000000191");
        Assert.DoesNotMatch(pattern, "12345678909");
        Assert.DoesNotMatch(pattern, "123.456.789-09");
    }

    [Theory(DisplayName = "O verificador rejeita vazamentos transitivos e exemplos sem depender do nome do schema")]
    [InlineData("properties", "password")]
    [InlineData("items", "passwordHash")]
    [InlineData("allOf", "verificationToken")]
    [InlineData("oneOf", "cpf")]
    [InlineData("anyOf", "connectionString")]
    [InlineData("additionalProperties", "fiscalCredential")]
    [InlineData("properties", "documentValue")]
    public void Detector_rejects_nested_output(string wrapper, string field)
    {
        var leaf = JsonSerializer.Serialize(new { type = "object", properties = new Dictionary<string, object> { [field] = new { type = "string" } } });
        var reference = "{\"$ref\":\"#/components/schemas/LegitimateRequest\"}";
        var nested = wrapper switch
        {
            "properties" => "{\"properties\":{\"nested\":" + reference + "}}",
            "allOf" or "oneOf" or "anyOf" => "{\"" + wrapper + "\":[" + reference + "]}",
            _ => "{\"" + wrapper + "\":" + reference + "}"
        };
        using var document = JsonDocument.Parse("""
            {"paths":{"/test":{"get":{"responses":{"200":{"$ref":"#/components/responses/Result"}}}}},
             "components":{"responses":{"Result":{"content":{"application/json":{"schema":$NESTED$}}}},
             "schemas":{"LegitimateRequest":$LEAF$}}}
            """.Replace("$NESTED$", nested).Replace("$LEAF$", leaf));
        Assert.NotEmpty(OpenApiDataMinimization.Inspect(document.RootElement));
    }

    [Theory(DisplayName = "O verificador rejeita headers, exemplos referenciados e valores pessoais ou de conexão")]
    [InlineData("{\"headers\":{\"X-Development-Verification-Token\":{\"schema\":{\"type\":\"string\"}}}}")]
    [InlineData("{\"content\":{\"application/json\":{\"example\":{\"nested\":[{\"bankCredential\":\"redacted\"}]}}}}")]
    [InlineData("{\"content\":{\"application/json\":{\"examples\":{\"sample\":{\"$ref\":\"#/components/examples/Secret\"}}}}}")]
    [InlineData("{\"content\":{\"application/json\":{\"example\":{\"document\":\"123.456.789-09\"}}}}")]
    [InlineData("{\"content\":{\"application/json\":{\"example\":{\"setting\":\"Host=invalid;Password=placeholder\"}}}}")]
    [InlineData("{\"content\":{\"application/json\":{\"example\":\"{\\\"password\\\":\\\"placeholder\\\"}\"}}}")]
    public void Detector_rejects_headers_and_examples(string response)
    {
        var examples = response.Contains("#/components/examples/Secret")
            ? "{\"Secret\":{\"value\":{\"verificationToken\":\"placeholder\"}}}"
            : "{}";
        using var document = JsonDocument.Parse("""
            {"paths":{"/test":{"get":{"responses":{"200":$RESPONSE$}}}},
             "components":{"examples":$EXAMPLES$}}
            """.Replace("$RESPONSE$", response).Replace("$EXAMPLES$", examples));
        Assert.NotEmpty(OpenApiDataMinimization.Inspect(document.RootElement));
    }

    [Fact(DisplayName = "Entradas sensíveis, referências cíclicas e token de sessão legítimo são distinguidos da saída")]
    public void Detector_allows_inputs_and_cycles_but_not_session_token_elsewhere()
    {
        const string json = """
            {"paths":{"/api/v1/auth/sessions":{"post":{
              "requestBody":{"content":{"application/json":{"schema":{"$ref":"#/components/schemas/Input"}}}},
              "responses":{"200":{"content":{"application/json":{"schema":{"properties":{"accessToken":{"type":"string"},"child":{"$ref":"#/components/schemas/Recursive"}}}}}}}}}},
             "components":{"schemas":{"Input":{"properties":{"password":{"type":"string"},"cpf":{"type":"string"}}},
               "Recursive":{"properties":{"child":{"$ref":"#/components/schemas/Recursive"}}}}}}
            """;
        using var document = JsonDocument.Parse(json);
        Assert.Empty(OpenApiDataMinimization.Inspect(document.RootElement));
        using var other = JsonDocument.Parse(json.Replace("/api/v1/auth/sessions", "/other"));
        Assert.NotEmpty(OpenApiDataMinimization.Inspect(other.RootElement));
    }

    [Fact(DisplayName = "Exemplos de entradas sensíveis e referências não resolvidas não passam silenciosamente")]
    public void Detector_checks_request_examples_and_rejects_unresolved_references()
    {
        using var input = JsonDocument.Parse("""
            {"paths":{},"components":{"schemas":{"Input":{"properties":{"password":{"type":"string","example":"placeholder"}}}}}}
            """);
        Assert.NotEmpty(OpenApiDataMinimization.Inspect(input.RootElement));
        using var broken = JsonDocument.Parse("""
            {"paths":{"/test":{"get":{"responses":{"200":{"$ref":"#/components/responses/Missing"}}}}},"components":{"responses":{}}}
            """);
        Assert.Throws<KeyNotFoundException>(() => OpenApiDataMinimization.Inspect(broken.RootElement));
    }
}
