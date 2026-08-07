# Tarefas: Gestão de Planos, Assinaturas e Billing do SaaS

## T-064 — Modelar Entidades de Assinatura e Planos (SaasPlan, TenantSubscription, SubscriptionInvoice) [pendente]

- Refs: US-041, US-042, US-043, AC-070, AC-074
- Arquivos: `src/Agendamento.Api/Domain/Subscriptions/SaasPlan.cs`, `src/Agendamento.Api/Domain/Subscriptions/TenantSubscription.cs`, `src/Agendamento.Api/Domain/Subscriptions/SubscriptionStatus.cs`, `src/Agendamento.Api/Domain/Subscriptions/PlanLimits.cs`
- Regra: Entidades para planos do SaaS, controle de cota/limites e status da assinatura com ciclo de Trial (14 dias).

## T-065 — Mapear Persistência EF Core e Migrations de Billing do SaaS [pendente]

- Refs: US-041, US-042, AC-070
- Arquivos: `src/Agendamento.Api/Infrastructure/Persistence/AgendamentoDbContext.cs`, `src/Agendamento.Api/Infrastructure/Persistence/Configurations/SaasPlanConfiguration.cs`, `src/Agendamento.Api/Infrastructure/Persistence/Configurations/TenantSubscriptionConfiguration.cs`
- Regra: Mapear tabelas `SaasPlans` e `TenantSubscriptions` no EF Core e gerar migration de billing.

## T-066 — Serviço de Validação de Cotas e Limites do Plano (PlanLimitsChecker) [pendente]

- Refs: US-043, AC-071
- Arquivos: `src/Agendamento.Api/Application/Subscriptions/Services/IPlanLimitsChecker.cs`, `src/Agendamento.Api/Infrastructure/Subscriptions/PlanLimitsChecker.cs`
- Regra: Validador reutilizável para checar se o tenant atingiu limite de usuários, orçamentos ou clientes antes de permitir a criação.

## T-067 — Caso de Uso: Processamento de Webhooks de Billing [pendente]

- Refs: US-044, AC-072, AC-073
- Arquivos: `src/Agendamento.Api/Application/Subscriptions/ProcessSubscriptionWebhook/ProcessSubscriptionWebhookHandler.cs`
- Regra: Handler para tratar eventos de pagamento (ex: `invoice.paid`, `invoice.payment_failed`) de forma idempotente atualizando o status do Tenant.

## T-068 — Caso de Uso: Consulta e Troca de Plano do Tenant [pendente]

- Refs: US-042, AC-070, AC-074
- Arquivos: `src/Agendamento.Api/Application/Subscriptions/GetTenantSubscription/`, `src/Agendamento.Api/Application/Subscriptions/ChangePlan/`
- Regra: Handlers para consultar o status atual da assinatura e iniciar fluxo de upgrade/downgrade de plano.

## T-069 — Expor Endpoints Minimal API de Billing e Webhooks [pendente]

- Refs: US-041, US-042, US-044, AC-070, AC-072
- Arquivos: `src/Agendamento.Api/Features/Subscriptions/SubscriptionEndpoints.cs`, `src/Agendamento.Api/Program.cs`
- Regra: Endpoints `/api/v1/subscriptions/current`, `/api/v1/subscriptions/plans` e endpoint público de Webhook `/api/v1/webhooks/billing`.

## T-070 — Testes de Integração de Validação de Cotas e Transições de Status [pendente]

- Refs: AC-070, AC-071, AC-072, AC-073, AC-074
- Arquivos: `tests/Agendamento.UnitTests/Subscriptions/SubscriptionDomainTests.cs`, `tests/Agendamento.IntegrationTests/Subscriptions/SubscriptionApiTests.cs`
- Regra: Testar a expiração automática de Trial, bloqueio de cota excedida e transição de status via webhook fictício.
