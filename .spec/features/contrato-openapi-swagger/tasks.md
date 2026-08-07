# Tasks: Contrato OpenAPI e Swagger

> feature: contrato-openapi-swagger

## T-022 — Registrar OpenAPI e Swagger UI [concluida]

- Refs: US-015, US-016, AC-036, AC-040
- Arquivos: src/Agendamento.Api/Agendamento.Api.csproj, src/Agendamento.Api/Program.cs, src/Agendamento.Api/OpenApi/OpenApiExtensions.cs, tests/Agendamento.IntegrationTests/OpenApi/SwaggerUiTests.cs
- Notas: UI apenas em Development/Test; documento em `/openapi/v1.json`.

## T-023 — Documentar endpoints, segurança e erros [concluida]

- Refs: US-015, AC-036, AC-037, AC-038
- Arquivos: src/Agendamento.Api/Features/Health, src/Agendamento.Api/Features/Identity, src/Agendamento.Api/Features/Tenants, src/Agendamento.Api/OpenApi/ProblemDetailsOperationTransformer.cs, src/Agendamento.Api/OpenApi/BearerSecuritySchemeTransformer.cs, tests/Agendamento.ArchitectureTests/OpenApi/EndpointMetadataTests.cs
- Notas: Toda operação possui identificador estável, tags, respostas e metadados de autenticação.

## T-024 — Gerar contrato canônico versionado [concluida]

- Refs: US-016, AC-039
- Arquivos: docs/openapi/v1.json, tools/export-openapi.ps1, tests/Agendamento.IntegrationTests/OpenApi/OpenApiSnapshotTests.cs
- Notas: Comparação semântica/canônica, sem depender de ordenação ou timestamps.

## T-025 — Validar completude e minimização dos schemas [concluida]

- Refs: US-015, US-016, AC-036, AC-037, AC-038, AC-039
- Arquivos: tests/Agendamento.ArchitectureTests/OpenApi/OpenApiCompletenessTests.cs, tests/Agendamento.ArchitectureTests/OpenApi/OpenApiSensitiveDataTests.cs
- Notas: Comparar endpoints reais com paths/métodos documentados e bloquear propriedades sensíveis em respostas/exemplos.
