# Tarefas: Gestão de Planos, Assinaturas e Billing do SaaS

## T-064 — Modelar Entidades de Assinatura e Planos (SaasPlan, TenantSubscription, SubscriptionInvoice) [concluida]

- Refs: US-201, US-202, US-203, AC-201, AC-205
- Arquivos: src/Agendamento.Api/Domain/Subscriptions/SaasPlan.cs, src/Agendamento.Api/Domain/Subscriptions/TenantSubscription.cs, src/Agendamento.Api/Domain/Subscriptions/SubscriptionStatus.cs, src/Agendamento.Api/Domain/Subscriptions/PlanLimits.cs
- Regra: Entidades para planos do SaaS, controle de cota/limites e status da assinatura com ciclo de Trial (14 dias).

## T-065 — Mapear Persistência EF Core e Migrations de Billing do SaaS [concluida]

- Refs: US-201, US-202, AC-201
- Arquivos: src/Agendamento.Api/Infrastructure/Persistence/AgendamentoDbContext.cs, src/Agendamento.Api/Infrastructure/Persistence/Configurations/SaasPlanConfiguration.cs, src/Agendamento.Api/Infrastructure/Persistence/Configurations/TenantSubscriptionConfiguration.cs
- Regra: Mapear tabelas `SaasPlans` e `TenantSubscriptions` no EF Core e gerar migration de billing.

## T-066 — Serviço de Validação de Cotas e Limites do Plano (PlanLimitsChecker) [concluida]

- Refs: US-203, AC-202
- Arquivos: src/Agendamento.Api/Application/Subscriptions/Services/IPlanLimitsChecker.cs, src/Agendamento.Api/Infrastructure/Subscriptions/PlanLimitsChecker.cs
- Regra: Validador reutilizável para checar se o tenant atingiu limite de usuários, orçamentos ou clientes antes de permitir a criação.

## T-067 — Caso de Uso: Processamento de Webhooks de Billing [concluida]

- Refs: US-204, AC-203, AC-204
- Arquivos: src/Agendamento.Api/Application/Subscriptions/ProcessSubscriptionWebhook/ProcessSubscriptionWebhookHandler.cs, src/Agendamento.Api/Application/Subscriptions/ProcessSubscriptionWebhook/ProcessSubscriptionWebhookCommand.cs
- Regra: Handler para tratar eventos de pagamento (ex: `invoice.paid`, `invoice.payment_failed`) de forma idempotente atualizando o status do Tenant.

## T-068 — Caso de Uso: Consulta e Troca de Plano do Tenant [concluida]

- Refs: US-202, AC-201, AC-205
- Arquivos: src/Agendamento.Api/Application/Subscriptions/GetTenantSubscription/GetTenantSubscriptionHandler.cs, src/Agendamento.Api/Application/Subscriptions/ChangePlan/ChangePlanHandler.cs
- Regra: Handlers para consultar o status atual da assinatura e iniciar fluxo de upgrade/downgrade de plano.

## T-069 — Expor Endpoints Minimal API de Billing e Webhooks [concluida]

- Refs: US-201, US-202, US-204, AC-201, AC-203
- Arquivos: src/Agendamento.Api/Features/Subscriptions/SubscriptionEndpoints.cs, src/Agendamento.Api/Program.cs
- Regra: Endpoints `/api/v1/subscriptions/current`, `/api/v1/subscriptions/plans` e endpoint público de Webhook `/api/v1/webhooks/billing`.

## T-070 — Testes de Integração de Validação de Cotas e Transições de Status [concluida]

- Refs: AC-201, AC-202, AC-203, AC-204, AC-205
- Arquivos: tests/Agendamento.UnitTests/Subscriptions/SubscriptionDomainTests.cs, tests/Agendamento.IntegrationTests/Subscriptions/SubscriptionApiTests.cs
- Regra: Testar a expiração automática de Trial, bloqueio de cota excedida e transição de status via webhook fictício.
