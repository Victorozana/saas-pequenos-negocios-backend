$ErrorActionPreference = 'Stop'
$previousUpdateSnapshots = $env:UPDATE_SNAPSHOTS
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$exportDirectory = Join-Path $repoRoot ('.local-verification/openapi-export-' + [Guid]::NewGuid().ToString('N'))
try {
    $env:UPDATE_SNAPSHOTS = 'true'
    & dotnet test (Join-Path $repoRoot 'tests/Agendamento.IntegrationTests/Agendamento.IntegrationTests.csproj') --configuration Release -p:UseAppHost=false --output (Join-Path $exportDirectory 'build') --filter 'FullyQualifiedName~OpenApiSnapshotTests' --results-directory $exportDirectory --logger 'trx;LogFileName=export.trx'
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao exportar o contrato pelo host InMemory de teste.' }
    Write-Host 'OpenAPI exportado para docs/openapi/v1.json pelo host InMemory de teste.'
} finally {
    $env:UPDATE_SNAPSHOTS = $previousUpdateSnapshots
}
