# Tasks: Onboarding sem exposição de token

> feature: onboarding-token-desenvolvimento

## T-101 — Remover exposição de token do cadastro e contrato [concluida]
- Refs: US-053, AC-100
- Arquivos: src/Agendamento.Api/OpenApi/OpenApiExtensions.cs, src/Agendamento.Api/Application/Tenants/RegisterTenant/RegisterTenantHandler.cs, src/Agendamento.Api/Features/Tenants/RegisterTenantResponse.cs, src/Agendamento.Api/Features/Tenants/TenantRegistrationEndpoints.cs, tests/Agendamento.ArchitectureTests/OpenApi/EndpointMetadataTests.cs, tests/Agendamento.IntegrationTests/Tenants/TenantOnboardingFlowTests.cs
- Notas: Regra histórica revisada por instrução explícita de 12/10/2026. Não ocultar propriedade por transformer: removê-la do runtime. Frontend do workspace recebe só mensagem e aceita código informado separadamente; entrega real de e-mail permanece pendente.
