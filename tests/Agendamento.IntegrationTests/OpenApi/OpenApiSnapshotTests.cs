using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Agendamento.IntegrationTests.Infrastructure;
using Xunit;
using Microsoft.Extensions.DependencyInjection;

namespace Agendamento.IntegrationTests.OpenApi;

public class OpenApiSnapshotTests : IClassFixture<AgendamentoApiFactory>
{
    private readonly AgendamentoApiFactory _factory;

    public OpenApiSnapshotTests(AgendamentoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "Contrato OpenAPI deve ser exportado e canônico @spec:AC-039")]
    public async Task OpenApiDocument_Should_Match_Snapshot()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to fetch openapi json: {response.StatusCode} - {err}");
        }

        var jsonString = await response.Content.ReadAsStringAsync();
        var actualNode = JsonNode.Parse(jsonString);
        var canonicalJson = actualNode?.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

        // Acha a pasta docs/openapi do projeto
        var currentDir = Directory.GetCurrentDirectory();
        var rootDir = currentDir;
        while (!Directory.Exists(Path.Combine(rootDir, "docs")))
        {
            var parent = Directory.GetParent(rootDir);
            if (parent == null) throw new Exception("Não foi possível encontrar a raiz do projeto com a pasta docs.");
            rootDir = parent.FullName;
        }

        var snapshotFilePath = Path.Combine(rootDir, "docs", "openapi", "v1.json");

        if (Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "true" || !File.Exists(snapshotFilePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(snapshotFilePath)!);
            await File.WriteAllTextAsync(snapshotFilePath, canonicalJson);
        }
        else
        {
            var expectedJsonString = await File.ReadAllTextAsync(snapshotFilePath);
            var expectedNode = JsonNode.Parse(expectedJsonString);
            var expectedCanonicalJson = expectedNode?.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

            Assert.Equal(expectedCanonicalJson, canonicalJson);
        }
    }
}
