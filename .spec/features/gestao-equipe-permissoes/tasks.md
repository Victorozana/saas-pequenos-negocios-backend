# Tarefas: Gestão de equipe e permissões

> feature: gestao-equipe-permissoes

Tarefas reabertas: a verificação anterior falhou e o formato antigo não era reconhecido pelo motor.

## T-080 — Atualizar entidade User com nome e DTO Profile [concluida]
- Refs: AC-070
- Arquivos: src/Agendamento.Api/Domain/Identity/User.cs, src/Agendamento.Api/Application/Tenants/RegisterTenant/RegisterTenantHandler.cs, src/Agendamento.Api/Features/Identity/CurrentUserEndpoints.cs, src/Agendamento.Api/Application/Identity/GetUserProfile/UserProfileResponse.cs

## T-081 — Definir Permissions e autorização em TenantMembership [concluida]
- Refs: AC-070, AC-071
- Arquivos: src/Agendamento.Api/Domain/Identity/Permissions.cs, src/Agendamento.Api/Domain/Tenants/TenantMembership.cs, src/Agendamento.Api/Infrastructure/Persistence/Configurations/TenantMembershipConfiguration.cs

## T-082 — Middleware/Handler de Permissões [concluida]
- Refs: AC-071
- Arquivos: src/Agendamento.Api/Infrastructure/Authorization/PermissionAuthorizationExtensions.cs, src/Agendamento.Api/Infrastructure/DependencyInjection.cs, src/Agendamento.Api/Infrastructure/Identity/EfIdentityStore.cs

## T-083 — Aplicar RequirePermission nos endpoints [concluida]
- Refs: AC-071
- Arquivos: src/Agendamento.Api/Features/Customers/CustomerEndpoints.cs, src/Agendamento.Api/Features/Services/ServiceEndpoints.cs, src/Agendamento.Api/Features/Quotations/QuotationEndpoints.cs, src/Agendamento.Api/Features/Appointments/AppointmentEndpoints.cs, src/Agendamento.Api/Features/Dashboard/DashboardEndpoints.cs, src/Agendamento.Api/Features/Financial/FinancialEndpoints.cs

## T-084 — Endpoints de gestão de equipe listar/status [concluida]
- Refs: AC-072, AC-073
- Arquivos: src/Agendamento.Api/Features/Team/TeamMemberEndpoints.cs, src/Agendamento.Api/Program.cs

## T-085 — Envio de convite e aceite [concluida]
- Refs: AC-074, AC-075
- Arquivos: src/Agendamento.Api/Features/Team/TeamMemberEndpoints.cs, src/Agendamento.Api/Domain/Tenants/TeamInvitation.cs, src/Agendamento.Api/Infrastructure/Persistence/Configurations/TeamInvitationConfiguration.cs, src/Agendamento.Api/Infrastructure/Tenancy/HttpTenantContext.cs, src/Agendamento.Api/Infrastructure/Notifications/MockCommunicationGateway.cs

## T-086 — Serialização de Enums e CORS [concluida]
- Refs: AC-070
- Arquivos: src/Agendamento.Api/Program.cs, src/Agendamento.Api/Infrastructure/DependencyInjection.cs

## T-087 — Configuração e validação de testes OpenAPI [concluida]
- Refs: AC-070
- Arquivos: tests/Agendamento.IntegrationTests/Features/TeamMemberTests.cs, tests/Agendamento.IntegrationTests/Features/TeamFinancialHttpTests.cs, tests/Agendamento.IntegrationTests/Features/TeamFinancialPostgreSqlTests.cs, tests/Agendamento.IntegrationTests/Infrastructure/AgendamentoApiFactory.cs
