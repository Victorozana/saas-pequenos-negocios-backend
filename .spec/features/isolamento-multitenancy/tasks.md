# Tasks: Isolamento multi-tenancy

> feature: isolamento-multitenancy

## T-017 — Resolver contexto de tenant autenticado [concluída]

- Refs: US-012, AC-028, AC-029
- Arquivos: src/Agendamento.Api/Application/Tenancy/ITenantContext.cs, src/Agendamento.Api/Infrastructure/Tenancy/HttpTenantContext.cs, src/Agendamento.Api/Infrastructure/Tenancy/TenantContextMiddleware.cs, tests/Agendamento.IntegrationTests/Tenancy/TenantContextTests.cs
- Notas: Aceitar tenant somente de claim confiável emitida pelo fluxo de autenticação.

## T-018 — Aplicar filtros e guarda de gravação do EF Core [concluída]

- Refs: US-013, AC-030, AC-031, AC-032
- Arquivos: src/Agendamento.Api/Domain/Common/ITenantOwned.cs, src/Agendamento.Api/Infrastructure/Persistence/AgendamentoDbContext.cs, src/Agendamento.Api/Infrastructure/Persistence/TenantSaveChangesInterceptor.cs, tests/Agendamento.IntegrationTests/Tenancy/EfTenantIsolationTests.cs
- Notas: Filtros globais cobrem todas as entidades `ITenantOwned`; interceptor preenche ausentes e rejeita divergências.

## T-019 — Criar papel, contexto transacional e policies RLS [concluída]

- Refs: US-014, AC-033, AC-034, AC-035
- Arquivos: src/Agendamento.Api/Infrastructure/Persistence/Migrations, src/Agendamento.Api/Infrastructure/Persistence/TenantTransactionInterceptor.cs, src/Agendamento.Api/Infrastructure/Persistence/Sql/TenantRls.sql, tests/Agendamento.IntegrationTests/Tenancy/PostgreSqlRlsTests.cs
- Notas: Usar `set_config('app.tenant_id', ..., true)` na mesma transação; papel da aplicação não possui `BYPASSRLS`.

## T-020 — Provar isolamento completo entre dois tenants [concluída]

- Refs: US-012, US-013, US-014, AC-028, AC-029, AC-030, AC-031, AC-032, AC-033, AC-034
- Arquivos: tests/Agendamento.IntegrationTests/Tenancy/CrossTenantIsolationTests.cs
- Notas: Cobrir HTTP, repository, EF interceptor, SQL direto e reuso do pool com tenants A/B.

## T-021 — Impedir migration tenant-owned desprotegida [concluída]

- Refs: US-014, AC-035
- Arquivos: tests/Agendamento.ArchitectureTests/Tenancy/TenantOwnedMigrationTests.cs
- Notas: Falhar quando o modelo/migration omitir coluna, obrigatoriedade, FK, índice iniciado por tenant ou policy RLS.
