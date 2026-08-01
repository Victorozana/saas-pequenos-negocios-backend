using System.Text.Json;
using Xunit;

namespace Agendamento.ArchitectureTests.Configuration;

public sealed class SecretConfigurationTests
{
    [Fact(DisplayName = "Configurações versionadas não contêm segredos reais @spec:AC-005")]
    public void Versioned_configuration_does_not_contain_real_secrets__spec_AC_005()
    {
        var configurationFiles = Directory
            .GetFiles(FindApiProjectDirectory(), "appsettings*.json", SearchOption.TopDirectoryOnly);

        Assert.NotEmpty(configurationFiles);

        foreach (var configurationFile in configurationFiles)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(configurationFile));
            AssertNoSecretValue(document.RootElement, configurationFile);
        }
    }

    private static void AssertNoSecretValue(JsonElement element, string configurationFile)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (IsSecretKey(property.Name))
                {
                    Assert.True(
                        property.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Object,
                        $"{configurationFile} contains a versioned secret in '{property.Name}'.");
                }

                AssertNoSecretValue(property.Value, configurationFile);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                AssertNoSecretValue(item, configurationFile);
            }
        }
    }

    private static bool IsSecretKey(string key) =>
        key.Contains("password", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("token", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("connectionstring", StringComparison.OrdinalIgnoreCase);

    private static string FindApiProjectDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Agendamento.Api");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("The Agendamento.Api project directory was not found.");
    }
}
