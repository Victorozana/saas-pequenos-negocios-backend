using Xunit;

namespace Agendamento.ArchitectureTests.Documentation;

public sealed class ReadmeDocumentationTests
{
    [Fact(DisplayName = "README apresenta o backend SaaS e sua execução local @spec:AC-102")]
    public void Readme_Should_Describe_CurrentBackendAndSafeLocalExecution()
    {
        var repositoryRoot = FindRepositoryRoot();
        var readme = File.ReadAllText(Path.Combine(repositoryRoot, "README.md"));

        Assert.Contains("SaaS para pequenos negócios", readme, StringComparison.Ordinal);
        Assert.Contains("http://localhost:5050", readme, StringComparison.Ordinal);
        Assert.Contains("/health", readme, StringComparison.Ordinal);
        Assert.Contains("/swagger", readme, StringComparison.Ordinal);
        Assert.Contains("/openapi/v1.json", readme, StringComparison.Ordinal);
        Assert.Contains("/api/v1/appointments", readme, StringComparison.Ordinal);
        Assert.Contains("/api/v1/financial", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("Senha@", readme, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Agendamento.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Não foi possível localizar a raiz do repositório.");
    }
}
