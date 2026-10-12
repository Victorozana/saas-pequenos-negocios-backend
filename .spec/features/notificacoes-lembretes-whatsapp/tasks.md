# Tarefas: Notificações e Lembretes WhatsApp

## T-097 — Serviço Abstrato e Entidades de Notificação [pendente]
- Refs: US-051, US-052, AC-097, AC-098
- Arquivos: src/Agendamento.Api/Infrastructure/Notifications/ICommunicationGateway.cs, src/Agendamento.Api/Domain/Notifications/NotificationMessage.cs, src/Agendamento.Api/Domain/Notifications/NotificationChannel.cs, src/Agendamento.Api/Domain/Notifications/NotificationStatus.cs, src/Agendamento.Api/Domain/Notifications/NotificationTemplate.cs, src/Agendamento.Api/Infrastructure/Notifications/MockCommunicationGateway.cs, src/Agendamento.Api/Infrastructure/Notifications/NotificationOutboxProcessor.cs
- Regra: Entidades de domínio `NotificationMessage` que será salva no banco. Serviço usando `ICommunicationGateway`.

## T-098 — Mapeamento EF Core de Notificações [pendente]
- Refs: AC-098
- Arquivos: src/Agendamento.Api/Infrastructure/Persistence/Configurations/NotificationMessageConfiguration.cs, src/Agendamento.Api/Infrastructure/Persistence/AgendamentoDbContext.cs
- Regra: Mapear `NotificationMessage` e configurar `IsTenantOwned`.

## T-099 — Integração de Notificação no Agendamento [concluida]

- Refs: US-051, AC-099
- Arquivos: src/Agendamento.Api/Application/Appointments/CreateAppointment/CreateAppointmentHandler.cs
- Regra: Ao agendar (no Handler existente de Appointments), despachar uma notificação para o serviço avisando que o agendamento foi criado.

## T-100 — Testes de Integração de Notificação [pendente]
- Refs: AC-097, AC-098, AC-099
- Arquivos: tests/Agendamento.IntegrationTests/Features/NotificationTests.cs
- Regra: Testar a gravação no banco e a injeção do serviço.

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

## T-104 — Implementar preferência de lembretes por cliente [pendente]

- Refs: US-051, AC-207
- Arquivos: src/Agendamento.Api/Domain/Customers/Customer.cs, src/Agendamento.Api/Features/Customers/CustomerEndpoints.cs, src/Agendamento.Api/Application/Appointments/CreateAppointment/CreateAppointmentHandler.cs, tests/Agendamento.IntegrationTests/Features/NotificationTests.cs
- Notas: Decisão de 12/10/2026; persistir preferência e respeitá-la na fila automática. Requer contrato e schema frontend sincronizados quando implementada.
