using System.Diagnostics;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace Agendamento.ArchitectureTests.Build;

public sealed class SolutionBuildTests
{
    private readonly ITestOutputHelper _output;

    public SolutionBuildTests(ITestOutputHelper output) => _output = output;

    [Fact(DisplayName = "@spec:AC-001 A solução compila em Release sem avisos")]
    public async Task Solution_compiles_in_release_without_warnings()
    {
        var solutionDirectory = FindSolutionDirectory();
        var artifactsDirectory = Path.Combine(Path.GetTempPath(), $"agendamento-build-{Guid.NewGuid():N}");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = solutionDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add("Agendamento.sln");
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add("Release");
        startInfo.ArgumentList.Add("--no-restore");
        startInfo.ArgumentList.Add("--nologo");
        startInfo.ArgumentList.Add("--disable-build-servers");
        // Projects share this disposable OutDir. Concurrent copies of common testhost
        // resources can collide on Windows and produce MSB3026 despite a valid build.
        startInfo.ArgumentList.Add("-maxcpucount:1");
        startInfo.ArgumentList.Add($"-p:OutDir={artifactsDirectory}{Path.DirectorySeparatorChar}");

        try
        {
            using var process = Assert.IsType<Process>(Process.Start(startInfo));

            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var output = (await standardOutput) + (await standardError);
            _output.WriteLine(output);
            Assert.True(process.ExitCode == 0, output);
            Assert.Matches(@"(?im)^\s*0 Warning\(s\)\s*$", output);
            Assert.DoesNotMatch(@"(?im):\s*warning\s+\w+", output);
        }
        finally
        {
            if (Directory.Exists(artifactsDirectory))
            {
                Directory.Delete(artifactsDirectory, recursive: true);
            }
        }
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
