# Tasks: Cadastro de tenants

> feature: cadastro-tenants

## T-010 — Modelar tenant, endereço e perfil fiscal [concluida]
- Refs: US-008, AC-018, AC-019, AC-020
- Arquivos: src/Agendamento.Api/Domain/Tenants/Tenant.cs, src/Agendamento.Api/Domain/Tenants/TenantAddress.cs, src/Agendamento.Api/Domain/Tenants/TenantFiscalProfile.cs, src/Agendamento.Api/Domain/Tenants/TaxRegime.cs, src/Agendamento.Api/Domain/Tenants/BusinessCategory.cs, tests/Agendamento.UnitTests/Tenants/TenantTests.cs, tests/Agendamento.UnitTests/Tenants/TenantFiscalProfileTests.cs
- Notas: O agregado não contém conta bancária, documentos ou credenciais fiscais.

## T-011 — Implementar CNPJ legado e alfanumérico [concluida]
- Refs: US-007, AC-014, US-009, AC-022
- Arquivos: src/Agendamento.Api/Domain/Tenants/Cnpj.cs, tests/Agendamento.UnitTests/Tenants/CnpjTests.cs
- Notas: Normalizar máscara/caixa, validar 14 posições e dígitos verificadores segundo a regra vigente da Receita.

## T-012 — Consultar elegibilidade cadastral [concluida]
- Refs: US-007, AC-015, AC-016, AC-017
- Arquivos: src/Agendamento.Api/Application/Tenants/CompanyRegistry/ICompanyRegistryGateway.cs, src/Agendamento.Api/Application/Tenants/CompanyRegistry/CompanyRegistryResult.cs, src/Agendamento.Api/Application/Tenants/CompanyRegistry/FoodCnaePolicy.cs, src/Agendamento.Api/Application/Tenants/LookupCompany/LookupCompanyQuery.cs, src/Agendamento.Api/Application/Tenants/LookupCompany/LookupCompanyHandler.cs, src/Agendamento.Api/Infrastructure/CompanyRegistry/CompanyRegistryGateway.cs, tests/Agendamento.UnitTests/Tenants/FoodCnaePolicyTests.cs, tests/Agendamento.IntegrationTests/Tenants/CompanyRegistryContractTests.cs
- Notas: CNAEs aceitos devem ser configuração versionada e testada; timeouts do provedor viram erro de dependência, nunca “CNPJ inválido”.

## T-013 — Mapear persistência e migration do cadastro [concluida]
- Refs: US-008, US-009, AC-019, AC-021, AC-022, AC-024
- Arquivos: src/Agendamento.Api/Infrastructure/Persistence/AgendamentoDbContext.cs, src/Agendamento.Api/Infrastructure/Persistence/Configurations/TenantConfiguration.cs, src/Agendamento.Api/Infrastructure/Persistence/Configurations/TenantFiscalProfileConfiguration.cs, src/Agendamento.Api/Infrastructure/Persistence/Configurations/TenantMembershipConfiguration.cs, src/Agendamento.Api/Infrastructure/Persistence/Migrations, tests/Agendamento.UnitTests/Tenants/TenantPersistenceTests.cs
- Notas: UUIDs, auditoria e unicidades são constraints do banco; membership usa FK explícita para usuário e tenant.

## T-014 — Orquestrar cadastro transacional e idempotente [concluida]
- Refs: US-009, AC-021, AC-022, AC-023, AC-024, US-005, AC-009
- Arquivos: src/Agendamento.Api/Application/Tenants/RegisterTenant/RegisterTenantCommand.cs, src/Agendamento.Api/Application/Tenants/RegisterTenant/RegisterTenantHandler.cs, src/Agendamento.Api/Application/Common/IUnitOfWork.cs, src/Agendamento.Api/Infrastructure/Persistence/IdempotencyRecord.cs, tests/Agendamento.UnitTests/Tenants/RegisterTenantHandlerTests.cs, tests/Agendamento.IntegrationTests/Tenants/RegisterTenantTransactionTests.cs, tests/Agendamento.IntegrationTests/Tenants/TenantOnboardingFlowTests.cs
- Notas: Uma transação inclui tenant, fiscal, usuário, membership, token e outbox de e-mail.

## T-015 — Expor consulta e cadastro públicos [concluida]
- Refs: US-007, US-008, US-009, US-011, AC-014, AC-015, AC-016, AC-017, AC-018, AC-020, AC-021, AC-023, AC-027
- Arquivos: src/Agendamento.Api/Features/Tenants/CompanyRegistryEndpoints.cs, src/Agendamento.Api/Features/Tenants/TenantRegistrationEndpoints.cs, src/Agendamento.Api/Features/Tenants/RegisterTenantRequest.cs, src/Agendamento.Api/Features/Tenants/RegisterTenantResponse.cs, src/Agendamento.Api/Features/Common/ProblemDetailsExtensions.cs, tests/Agendamento.IntegrationTests/Tenants/TenantRegistrationEndpointTests.cs
- Notas: Implementar GET de consulta e POST de cadastro; aplicar rate limit e exigir `Idempotency-Key` no POST.

## T-016 — Expor perfis autenticados [concluida]
- Refs: US-010, AC-025, AC-026
- Arquivos: src/Agendamento.Api/Application/Tenants/GetCurrentTenant/GetCurrentTenantQuery.cs, src/Agendamento.Api/Application/Identity/GetCurrentUser/GetCurrentUserQuery.cs, src/Agendamento.Api/Features/Tenants/CurrentTenantEndpoints.cs, src/Agendamento.Api/Features/Identity/CurrentUserEndpoints.cs, tests/Agendamento.IntegrationTests/Tenants/CurrentTenantEndpointTests.cs, tests/Agendamento.IntegrationTests/Identity/CurrentUserEndpointTests.cs
- Notas: Implementar `GET /api/v1/tenants/me` e `GET /api/v1/users/me` sem aceitar tenant no request.
