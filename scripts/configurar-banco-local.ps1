param(
    [string]$Database = 'agendamento_local',
    [string]$Username = 'postgres',
    [int]$Port = 5432
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '../src/Agendamento.Api/Agendamento.Api.csproj'
$securePassword = Read-Host 'Senha do PostgreSQL (entrada oculta)' -AsSecureString
$passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
try {
    $password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
    # Quote every value to safely support semicolons and quotes in passwords.
    function Quote-ConnectionValue([string]$value) {
        return '"' + $value.Replace('"', '""') + '"'
    }
    $connection = 'Host=localhost;Port=' + $Port + ';Database=' + (Quote-ConnectionValue $Database) + ';Username=' + (Quote-ConnectionValue $Username) + ';Password=' + (Quote-ConnectionValue $password)
    $settings = @{
        'Database:RequirePostgreSql' = 'true'
        'ConnectionStrings:PostgreSql' = $connection
    } | ConvertTo-Json -Compress
    # Send secrets through stdin, keeping the password out of command arguments.
    $settings | & dotnet user-secrets set --project $project | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Nao foi possivel guardar os segredos locais.' }
    Write-Host 'Conexao salva nos segredos locais do .NET. Aplicando migrations...'
    & dotnet ef database update --project $project *> $null
    if ($LASTEXITCODE -ne 0) {
        throw 'Falha nas migrations. Confira servidor, porta, usuario, senha e nome do banco. A configuracao foi salva; nao inicie a API ate concluir as migrations.'
    }
    Write-Host 'Tabelas atualizadas. Inicie a API com o perfil http (Development).'
}
finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
    $password = $null
    $connection = $null
    $settings = $null
    $securePassword.Dispose()
}
