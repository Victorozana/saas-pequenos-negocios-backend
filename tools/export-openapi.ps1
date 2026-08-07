$env:UPDATE_SNAPSHOTS="true"
dotnet test tests/Agendamento.IntegrationTests/Agendamento.IntegrationTests.csproj --filter "FullyQualifiedName~OpenApiSnapshotTests"
$env:UPDATE_SNAPSHOTS="false"
Write-Host "OpenAPI exportado para docs/openapi/v1.json"
