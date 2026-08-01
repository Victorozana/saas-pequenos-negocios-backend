# Tasks: Fundação da API e dos testes

> feature: fundacao-api-testes

## T-001 — Organizar projetos e dependências .NET [concluida]
- Refs: US-001, AC-001
- Arquivos: Agendamento.sln, Directory.Packages.props, src/Agendamento.Api/Agendamento.Api.csproj, src/Agendamento.Api/Application/DependencyInjection.cs, tests/Agendamento.UnitTests/Agendamento.UnitTests.csproj, tests/Agendamento.IntegrationTests/Agendamento.IntegrationTests.csproj, tests/Agendamento.ArchitectureTests/Agendamento.ArchitectureTests.csproj
- Notas: Fixar versões compatíveis com .NET 10 e manter `TreatWarningsAsErrors`.
- Modelo: gpt-5.6-terra
- Esforço: medio

## T-002 — Criar host e prova HTTP reutilizável [concluida]
- Refs: US-001, US-002, AC-002, AC-003
- Arquivos: tests/Agendamento.IntegrationTests/Infrastructure/AgendamentoApiFactory.cs, tests/Agendamento.IntegrationTests/Health/HealthEndpointTests.cs
- Notas: O título do teste de saúde deve conter `@spec:AC-002`.
- Modelo: gpt-5.6-terra
- Esforço: medio

## T-003 — Criar infraestrutura PostgreSQL isolada [concluida]
- Refs: US-002, AC-004
- Arquivos: tests/Agendamento.IntegrationTests/Infrastructure/PostgreSqlFixture.cs, tests/Agendamento.IntegrationTests/Infrastructure/PostgreSqlCollection.cs, tests/Agendamento.IntegrationTests/Persistence/PostgreSqlFixtureTests.cs
- Notas: Provisionar PostgreSQL por Testcontainers, aplicar migrations e limpar recursos ao final.
- Modelo: gpt-5.6-sol
- Esforço: alto

## T-004 — Externalizar e validar configuração [concluida]
- Refs: US-003, AC-005, AC-006
- Arquivos: src/Agendamento.Api/appsettings.json, src/Agendamento.Api/appsettings.Development.json, src/Agendamento.Api/Infrastructure/DependencyInjection.cs, tests/Agendamento.ArchitectureTests/Configuration/SecretConfigurationTests.cs, tests/Agendamento.IntegrationTests/Configuration/DatabaseConfigurationTests.cs, onpspec.config.json
- Notas: Ajustar o motor ONP para executar `dotnet test Agendamento.sln`; mensagens de erro não podem ecoar strings de conexão.
- Modelo: gpt-5.6-terra
- Esforço: medio
