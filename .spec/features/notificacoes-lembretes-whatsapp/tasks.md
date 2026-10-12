# Tarefas: Notificações e Lembretes WhatsApp

## T-097 — Serviço Abstrato e Entidades de Notificação [concluida]
- Refs: US-051, US-052, AC-097, AC-098
- Arquivos: src/Agendamento.Api/Application/Notifications/INotificationService.cs, src/Agendamento.Api/Infrastructure/Notifications/NotificationService.cs, src/Agendamento.Api/Domain/Notifications/NotificationAudience.cs, src/Agendamento.Api/Infrastructure/DependencyInjection.cs, src/Agendamento.Api/Infrastructure/Notifications/ICommunicationGateway.cs, src/Agendamento.Api/Domain/Notifications/NotificationMessage.cs, src/Agendamento.Api/Domain/Notifications/NotificationChannel.cs, src/Agendamento.Api/Domain/Notifications/NotificationStatus.cs, src/Agendamento.Api/Domain/Notifications/NotificationTemplate.cs, src/Agendamento.Api/Infrastructure/Notifications/MockCommunicationGateway.cs, src/Agendamento.Api/Infrastructure/Notifications/NotificationOutboxProcessor.cs
- Regra: INotificationService centraliza validação de cliente, staging de outbox e despacho; ICommunicationGateway permanece adaptador substituível do provedor.

## T-098 — Mapeamento EF Core de Notificações [concluida]
- Refs: AC-098
- Arquivos: src/Agendamento.Api/Infrastructure/Persistence/Configurations/NotificationMessageConfiguration.cs, src/Agendamento.Api/Infrastructure/Persistence/AgendamentoDbContext.cs
- Regra: Mapear tenant/cliente, CHECK de Audience e FK composta com Restrict, preservando legado.
- Migration: src/Agendamento.Api/Infrastructure/Persistence/Migrations/20261012154514_AddNotificationCustomerHistory.cs

## T-099 — Integração de Notificação no Agendamento [concluida]

- Refs: US-051, AC-099
- Arquivos: src/Agendamento.Api/Application/Appointments/CreateAppointment/CreateAppointmentHandler.cs
- Regra: Ao agendar (no Handler existente de Appointments), despachar uma notificação para o serviço avisando que o agendamento foi criado.

## T-100 — Testes de Integração de Notificação [concluida]
- Refs: AC-097, AC-098, AC-099
- Arquivos: tests/Agendamento.IntegrationTests/Features/NotificationTests.cs, tests/Agendamento.IntegrationTests/Notifications/NotificationServiceTests.cs, tests/Agendamento.IntegrationTests/Notifications/NotificationHistoryHttpTests.cs, tests/Agendamento.IntegrationTests/Notifications/NotificationHistoryPostgreSqlTests.cs
- Regra: Provar centralização, tenant/cliente, fila/retries/templates e migration histórica em bases temporárias.

## T-052 — Modelar Entidades de Notificação e Outbox (NotificationMessage, OutboxMessage) [pendente]

- Refs: US-033, US-035, US-036, AC-060, AC-061, AC-063
- Arquivos: src/Agendamento.Api/Domain/Notifications/NotificationMessage.cs, src/Agendamento.Api/Domain/Notifications/NotificationStatus.cs, src/Agendamento.Api/Domain/Notifications/NotificationChannel.cs, src/Agendamento.Api/Domain/Notifications/NotificationTemplate.cs
- Regra: Estrutura de mensagens com suporte a canais (`WhatsApp`, `Email`), contagem de retries e status de entrega.

## T-053 — Mapear Persistência EF Core e Outbox Pattern [pendente]

- Refs: US-035, AC-060, AC-061
- Arquivos: src/Agendamento.Api/Infrastructure/Persistence/AgendamentoDbContext.cs, src/Agendamento.Api/Infrastructure/Persistence/Configurations/NotificationMessageConfiguration.cs
- Regra: Mapeamento da tabela `NotificationMessages` com isolamento RLS por `TenantId` e índices para o worker de processamento de fila.

## T-054 — Implementar Provedor de Envio de Mensagens e Dispatcher Background [pendente]

- Refs: US-033, US-034, US-035, AC-061, AC-063
- Arquivos: src/Agendamento.Api/Infrastructure/Notifications/ICommunicationGateway.cs, src/Agendamento.Api/Infrastructure/Notifications/NotificationOutboxProcessor.cs
- Regra: Abstração de gateway de mensagens WhatsApp/SMS/Email e BackgroundService para polling e despacho resiliente da fila de outbox.

## T-055 — Casos de Uso: Agendamento de Lembretes e Notificação de Orçamento [pendente]

- Refs: US-033, US-034, AC-062
- Arquivos: src/Agendamento.Api/Application/Notifications/ScheduleAppointmentReminder/ScheduleAppointmentReminderCommand.cs, src/Agendamento.Api/Application/Notifications/ScheduleAppointmentReminder/ScheduleAppointmentReminderHandler.cs, src/Agendamento.Api/Application/Notifications/SendQuotationNotification/SendQuotationNotificationCommand.cs, src/Agendamento.Api/Application/Notifications/SendQuotationNotification/SendQuotationNotificationHandler.cs
- Regra: Handlers para criar mensagens formatadas utilizando templates parametrizados do tenant.

## T-056 — Expor Endpoints Minimal API de Notificações e Histórico [pendente]

- Refs: US-036, AC-060, AC-064
- Arquivos: src/Agendamento.Api/Features/Notifications/NotificationEndpoints.cs, src/Agendamento.Api/Program.cs
- Regra: Endpoints `/api/v1/notifications` para consultar histórico de envios e disparar notificações manuais se necessário.

## T-057 — Testes de Integração do Outbox Processor e Formatação de Mensagens [pendente]

- Refs: AC-060, AC-061, AC-062, AC-063
- Arquivos: tests/Agendamento.UnitTests/Notifications/NotificationDomainTests.cs, tests/Agendamento.IntegrationTests/Notifications/NotificationApiTests.cs, src/Agendamento.Api/Infrastructure/Notifications/MockCommunicationGateway.cs
- Regra: Testar a renderização de templates, estratégia de retry em falhas e isolamento por tenant.

## T-104 — Implementar preferência de lembretes por cliente [concluida]
- Refs: US-051, AC-207
- Arquivos: src/Agendamento.Api/Domain/Customers/Customer.cs, src/Agendamento.Api/Features/Customers/CustomerEndpoints.cs, src/Agendamento.Api/Application/Customers/CreateCustomer/CreateCustomerCommand.cs, src/Agendamento.Api/Application/Customers/CreateCustomer/CreateCustomerHandler.cs, src/Agendamento.Api/Application/Customers/UpdateCustomer/UpdateCustomerCommand.cs, src/Agendamento.Api/Application/Customers/UpdateCustomer/UpdateCustomerHandler.cs, src/Agendamento.Api/Application/Customers/GetCustomers/GetCustomerByIdHandler.cs, src/Agendamento.Api/Application/Customers/GetCustomers/GetCustomersHandler.cs, src/Agendamento.Api/Infrastructure/Persistence/Configurations/CustomerConfiguration.cs, src/Agendamento.Api/Application/Notifications/INotificationService.cs, src/Agendamento.Api/Infrastructure/Notifications/NotificationService.cs, src/Agendamento.Api/Application/Notifications/ScheduleAppointmentReminder/ScheduleAppointmentReminderHandler.cs, src/Agendamento.Api/Application/Appointments/CreateAppointment/CreateAppointmentHandler.cs, tests/Agendamento.IntegrationTests/Notifications/AutomaticReminderPreferenceTests.cs, tests/Agendamento.IntegrationTests/Notifications/AutomaticReminderPreferenceHttpTests.cs, tests/Agendamento.IntegrationTests/Notifications/AutomaticReminderPreferencePostgreSqlTests.cs, tests/Agendamento.IntegrationTests/Features/NotificationTests.cs
- Notas: Decisão de 12/10/2026; persistir preferência e respeitá-la na fila automática. Requer contrato e schema frontend sincronizados quando implementada.

## T-105 — Implementar aviso automático diário de conta vencida [concluida]
- Refs: US-052, AC-208
- Arquivos: src/Agendamento.Api/Domain/Financial/ReceivableTitle.cs, src/Agendamento.Api/Application/Notifications/INotificationService.cs, src/Agendamento.Api/Infrastructure/Notifications/NotificationService.cs, src/Agendamento.Api/Infrastructure/Notifications/NotificationOutboxProcessor.cs, src/Agendamento.Api/Domain/Notifications/NotificationMessage.cs, src/Agendamento.Api/Infrastructure/Persistence/Configurations/NotificationMessageConfiguration.cs, tests/Agendamento.IntegrationTests/Notifications/NotificationServiceTests.cs, src/Agendamento.Api/Infrastructure/Notifications/OverdueReceivableReminderProcessor.cs, src/Agendamento.Api/Infrastructure/Notifications/OverdueReceivableReminderWorker.cs, src/Agendamento.Api/Infrastructure/DependencyInjection.cs, tests/Agendamento.IntegrationTests/Notifications/OverdueReceivableReminderPostgreSqlTests.cs
- Regra: Processar diariamente contas vencidas com saldo aberto; no máximo um aviso por tenant/conta/dia até quitar ou cancelar, com deduplicação durável, data controlável, opt-out de cliente, isolamento e retries da mesma mensagem.
- Notas: Disparo e repetição confirmados pelo usuário em 12/10/2026. Implementação e quatro provas PostgreSQL PASS; verify completo motor=0/runner=0, 224 resultados. Migration 20261012191900_AddDailyReceivableReminderDeduplication somente em bases temporárias exclusivas. Nenhuma aplicação ao banco de trabalho. Ver .spec/revisao-aviso-diario.md.
