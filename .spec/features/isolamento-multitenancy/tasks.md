# Tasks: Isolamento multi-tenancy

> feature: isolamento-multitenancy

## T-017 — Resolver contexto de tenant autenticado [concluída]

- Refs: US-012, AC-028, AC-029
- Arquivos: src/Agendamento.Api/Application/Tenancy/ITenantContext.cs, src/Agendamento.Api/Infrastructure/Tenancy/HttpTenantContext.cs, src/Agendamento.Api/Infrastructure/Tenancy/TenantContextMiddleware.cs, tests/Agendamento.IntegrationTests/Tenancy/TenantContextTests.cs
- Notas: Aceitar tenant somente de claim confiável emitida pelo fluxo de autenticação.

## T-018 — Aplicar filtros e guarda de gravação do EF Core [concluída]

- Refs: US-013, AC-030, AC-031, AC-032
- Arquivos: src/Agendamento.Api/Domain/Common/ITenantOwned.cs, src/Agendamento.Api/Infrastructure/Persistence/AgendamentoDbContext.cs, src/Agendamento.Api/Infrastructure/Persistence/TenantSaveChangesInterceptor.cs, tests/Agendamento.IntegrationTests/Tenancy/EfTenantIsolationTests.cs, tests/Agendamento.IntegrationTests/Tenancy/TenantWriteGuardTests.cs
- Notas: Filtros globais cobrem todas as entidades `ITenantOwned`; interceptor preenche ausentes e rejeita divergências.

## T-019 — Criar papel, contexto transacional e policies RLS [concluida]
- Refs: US-014, AC-033, AC-034, AC-035
- Arquivos: src/Agendamento.Api/Infrastructure/Persistence/Migrations/20260807002238_InitialTenant.cs, src/Agendamento.Api/Infrastructure/Persistence/Migrations/20260807002238_InitialTenant.Designer.cs, src/Agendamento.Api/Infrastructure/Persistence/Migrations/20260807010000_AddRowLevelSecurity.cs, src/Agendamento.Api/Infrastructure/Persistence/Migrations/20260807225823_AddCustomerTable.cs, src/Agendamento.Api/Infrastructure/Persistence/Migrations/20260807225823_AddCustomerTable.Designer.cs, src/Agendamento.Api/Infrastructure/Persistence/Migrations/20260807231536_AddQuotationsAndServices.cs, src/Agendamento.Api/Infrastructure/Persistence/Migrations/20260807231536_AddQuotationsAndServices.Designer.cs, src/Agendamento.Api/Infrastructure/Persistence/Migrations/20260808001400_AddWorkOrdersAndAppointments.cs, src/Agendamento.Api/Infrastructure/Persistence/Migrations/20260808001400_AddWorkOrdersAndAppointments.Designer.cs, src/Agendamento.Api/Infrastructure/Persistence/Migrations/20260808005011_AddFinancialTables.cs, src/Agendamento.Api/Infrastructure/Persistence/Migrations/20260808005011_AddFinancialTables.Designer.cs, src/Agendamento.Api/Infrastructure/Persistence/Migrations/20260809234023_AddNotificationModule.cs, src/Agendamento.Api/Infrastructure/Persistence/Migrations/20260809234023_AddNotificationModule.Designer.cs, src/Agendamento.Api/Infrastructure/Persistence/Migrations/20260810001025_AddBillingAndSubscriptions.cs, src/Agendamento.Api/Infrastructure/Persistence/Migrations/20260810001025_AddBillingAndSubscriptions.Designer.cs, src/Agendamento.Api/Infrastructure/Persistence/Migrations/20260810231913_UpdateNotificationsBackoff.cs, src/Agendamento.Api/Infrastructure/Persistence/Migrations/20260810231913_UpdateNotificationsBackoff.Designer.cs, src/Agendamento.Api/Infrastructure/Persistence/Migrations/20260811235746_AddEmailVerificationTokens.cs, src/Agendamento.Api/Infrastructure/Persistence/Migrations/20260811235746_AddEmailVerificationTokens.Designer.cs, src/Agendamento.Api/Infrastructure/Persistence/Migrations/20261012002349_AddActiveTeamMembership.cs, src/Agendamento.Api/Infrastructure/Persistence/Migrations/20261012002349_AddActiveTeamMembership.Designer.cs, src/Agendamento.Api/Infrastructure/Persistence/Migrations/AgendamentoDbContextModelSnapshot.cs, src/Agendamento.Api/Infrastructure/Persistence/TenantTransactionInterceptor.cs, src/Agendamento.Api/Infrastructure/Persistence/Sql/TenantRls.sql, tests/Agendamento.IntegrationTests/Tenancy/PostgreSqlRlsTests.cs, src/Agendamento.Api/Infrastructure/Persistence/UnitOfWork.cs, src/Agendamento.Api/Infrastructure/Persistence/Migrations/20261012041401_EnforceTenantRowLevelSecurity.cs, src/Agendamento.Api/Infrastructure/Persistence/Migrations/20261012041401_EnforceTenantRowLevelSecurity.Designer.cs, src/Agendamento.Api/Infrastructure/Persistence/AdministrativeDatabaseScope.cs, src/Agendamento.Api/Infrastructure/Persistence/TenantConnectionInterceptor.cs, src/Agendamento.Api/Infrastructure/DependencyInjection.cs, src/Agendamento.Api/Infrastructure/Persistence/AgendamentoDbContextFactory.cs, src/Agendamento.Api/Infrastructure/Persistence/Configurations/WorkOrderConfiguration.cs, src/Agendamento.Api/Infrastructure/Persistence/Configurations/ReceivableTitleConfiguration.cs, src/Agendamento.Api/Infrastructure/Persistence/Configurations/PayableTitleConfiguration.cs, src/Agendamento.Api/Infrastructure/Identity/EfIdentityStore.cs, src/Agendamento.Api/Infrastructure/Persistence/DbSeeder.cs, src/Agendamento.Api/Infrastructure/Notifications/NotificationOutboxProcessor.cs, src/Agendamento.Api/Application/Tenants/RegisterTenant/RegisterTenantHandler.cs, src/Agendamento.Api/Features/Team/TeamMemberEndpoints.cs, tests/Agendamento.IntegrationTests/Features/TeamFinancialHttpTests.cs, tests/Agendamento.IntegrationTests/Features/TeamFinancialPostgreSqlTests.cs
- Notas: Usar `set_config('app.tenant_id', ..., true)` na mesma transação; papel da aplicação não possui `BYPASSRLS`.

## T-020 — Provar isolamento completo entre dois tenants [concluida]
- Refs: US-012, US-013, US-014, AC-028, AC-029, AC-030, AC-031, AC-032, AC-033, AC-034
- Arquivos: tests/Agendamento.IntegrationTests/Tenancy/CrossTenantIsolationTests.cs
- Notas: Cobrir HTTP, repository, EF interceptor, SQL direto e reuso do pool com tenants A/B.

## T-021 — Impedir migration tenant-owned desprotegida [concluida]
- Refs: US-014, AC-035
- Arquivos: tests/Agendamento.ArchitectureTests/Tenancy/TenantOwnedMigrationTests.cs
- Notas: Falhar quando o modelo/migration omitir coluna, obrigatoriedade, FK, índice iniciado por tenant ou policy RLS.

## Revisão de 12/10/2026

- T-019, T-020 e T-021 foram reabertas: falta prova PostgreSQL/RLS e há arquivos planejados ausentes.
- A migration AddRowLevelSecurity existe como fonte, mas não aparece na lista de migrations descoberta pelo EF Core compilado. Não foi aplicada nenhuma migration nesta revisão.
- A UnitOfWork configura agendamento.current_tenant_id somente em sua transação explícita; a spec/design pede app.tenant_id. A transação não é adotada por todas as consultas e gravações.
- Os novos testes TenantWriteGuardTests provam filtros EF e rejeição de gravação divergente, incluindo troca do TenantId em entidade carregada. Eles não certificam SQL direto, privilégios ou pool.

## Implementação RLS desta retomada

- Migration nova após AddActiveTeamMembership, descoberta pelo EF; a fonte histórica AddRowLevelSecurity continua sem descoberta e não foi ativada fora de ordem.
- 16 tabelas protegidas, incluindo QuotationItem, WorkOrderItems e pagamentos; colunas físicas tenant_id, FKs, índices, ENABLE/FORCE RLS e USING/WITH CHECK.
- Backfill dos dependentes com tenant do agregado e FKs compostas para impedir referências entre tenants. Registros antigos preservados em prova PostgreSQL.
- Toda consulta/gravação EF usa transação e contexto local, inclusive SQL e caminhos sem UnitOfWork. Transações dos handlers permanecem sob responsabilidade dos handlers.
- Credenciais normais, administrativas e de migration separadas; API recusa superuser/BYPASSRLS/proprietário. Papel normal não pode assumir o de sistema.
- Cadastro, autenticação, validação de sessão, aceite de convite e outbox são portas administrativas explícitas. Webhook billing anônimo não recebeu acesso administrativo; gateway segue futuro.
- Não houve aplicação ao banco de trabalho nem alteração de segredos locais. Provisionamento das credenciais e aplicação local permanecem fora da validação temporária.
