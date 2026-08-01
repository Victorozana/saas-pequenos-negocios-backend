using Agendamento.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Agendamento.IntegrationTests.Configuration;

public sealed class DatabaseConfigurationTests
{
    [Fact(DisplayName = "PostgreSQL obrigatório falha sem expor credenciais @spec:AC-006")]
    public void Required_PostgreSql_without_valid_connection_string_fails_without_echoing_credentials__spec_AC_006()
    {
        const string configuredConnectionString = "Password=not-a-real-secret";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:RequirePostgreSql"] = "true",
                ["ConnectionStrings:PostgreSql"] = configuredConnectionString,
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddInfrastructure(configuration));

        Assert.Contains("PostgreSQL configuration is invalid", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(configuredConnectionString, exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("not-a-real-secret", exception.ToString(), StringComparison.Ordinal);
    }
}
