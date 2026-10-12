$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$rawDirectory = Join-Path $repoRoot '.spec/verification/raw'
New-Item -ItemType Directory -Path $rawDirectory -Force | Out-Null
$projects = @('Agendamento.UnitTests', 'Agendamento.IntegrationTests', 'Agendamento.ArchitectureTests')
$verificationRunDirectory = if ($env:SPEC_VERIFY_RUN_DIRECTORY) {
    [IO.Path]::GetFullPath($env:SPEC_VERIFY_RUN_DIRECTORY)
} else {
    Join-Path $repoRoot ('.local-verification/spec-runs/' + [Guid]::NewGuid().ToString('N'))
}
New-Item -ItemType Directory -Path $verificationRunDirectory -Force | Out-Null
$assertions = @()
$failed = $false
foreach ($projectName in $projects) {
    $project = Join-Path $repoRoot "tests/$projectName/$projectName.csproj"
    $outputDirectory = Join-Path $verificationRunDirectory "$projectName/build"
    $trxName = "$projectName.trx"
    $trxPath = Join-Path $rawDirectory $trxName
    if (Test-Path -LiteralPath $trxPath) { Remove-Item -LiteralPath $trxPath }
    $previousErrorPreference = $ErrorActionPreference
    try {
        # Windows PowerShell represents native stderr as ErrorRecord. A failing
        # test must still allow the other suites and current granular JSON to run.
        $ErrorActionPreference = 'Continue'
        & dotnet test $project --configuration Release -p:UseAppHost=false --output $outputDirectory --results-directory $rawDirectory --logger "trx;LogFileName=$trxName" --logger 'console;verbosity=quiet' 2>&1 | Tee-Object -FilePath (Join-Path $verificationRunDirectory "$projectName.log")
        $projectExitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousErrorPreference
    }
    if ($projectExitCode -ne 0) { $failed = $true }
    if (!(Test-Path -LiteralPath $trxPath)) { $failed = $true; continue }
    [xml]$trx = Get-Content -LiteralPath $trxPath -Raw
    $namespaces = New-Object System.Xml.XmlNamespaceManager($trx.NameTable)
    $namespaces.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
    foreach ($result in $trx.SelectNodes('//t:UnitTestResult', $namespaces)) {
        $status = switch ($result.outcome) { 'Passed' { 'passed' } 'NotExecuted' { 'skipped' } default { 'failed' } }
        $assertions += @{ fullName = [string]$result.testName; title = [string]$result.testName; status = $status }
    }
}
$report = @{ testResults = @(@{ assertionResults = @($assertions) }) } | ConvertTo-Json -Depth 8
[IO.File]::WriteAllText((Join-Path $repoRoot '.spec/verification/test-results.json'), $report, (New-Object System.Text.UTF8Encoding($false)))
if ($failed) { exit 1 }
exit 0
