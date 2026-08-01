using System.Diagnostics;
using System.Text.RegularExpressions;
using Xunit;

namespace Agendamento.ArchitectureTests.Build;

public sealed class SolutionBuildTests
{
    [Fact(DisplayName = "@spec:AC-001 A solução compila em Release sem avisos")]
    public async Task Solution_compiles_in_release_without_warnings()
    {
        var solutionDirectory = FindSolutionDirectory();
        var startInfo = new ProcessStartInfo("dotnet", "build Agendamento.sln --configuration Release --nologo")
        {
            WorkingDirectory = solutionDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Assert.IsType<Process>(Process.Start(startInfo));

        var standardOutput = await process.StandardOutput.ReadToEndAsync();
        var standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var output = standardOutput + standardError;
        Assert.True(process.ExitCode == 0, output);
        Assert.Matches(@"(?im)^\s*0 Warning\(s\)\s*$", output);
        Assert.DoesNotMatch(@"(?im):\s*warning\s+\w+", output);
    }

    private static string FindSolutionDirectory()
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Agendamento.sln"))) return directory.FullName;
        }

        throw new DirectoryNotFoundException("A raiz que contém Agendamento.sln não foi encontrada.");
    }
}
