# Tarefas: Notificações, Lembretes e Integração WhatsApp

## T-052 — Modelar Entidades de Notificação e Outbox (NotificationMessage, OutboxMessage) [concluida]

- Refs: US-033, US-035, US-036, AC-060, AC-061, AC-063
- Arquivos: `src/Agendamento.Api/Domain/Notifications/NotificationMessage.cs`, `src/Agendamento.Api/Domain/Notifications/NotificationStatus.cs`, `src/Agendamento.Api/Domain/Notifications/NotificationChannel.cs`, `src/Agendamento.Api/Domain/Notifications/NotificationTemplate.cs`
- Regra: Estrutura de mensagens com suporte a canais (`WhatsApp`, `Email`), contagem de retries e status de entrega.

## T-053 — Mapear Persistência EF Core e Outbox Pattern [concluida]

- Refs: US-035, AC-060, AC-061
- Arquivos: `src/Agendamento.Api/Infrastructure/Persistence/AgendamentoDbContext.cs`, `src/Agendamento.Api/Infrastructure/Persistence/Configurations/NotificationMessageConfiguration.cs`
- Regra: Mapeamento da tabela `NotificationMessages` com isolamento RLS por `TenantId` e índices para o worker de processamento de fila.

## T-054 — Implementar Provedor de Envio de Mensagens e Dispatcher Background [concluida]

- Refs: US-033, US-034, US-035, AC-061, AC-063
- Arquivos: `src/Agendamento.Api/Infrastructure/Notifications/ICommunicationGateway.cs`, `src/Agendamento.Api/Infrastructure/Notifications/NotificationOutboxProcessor.cs`
- Regra: Abstração de gateway de mensagens WhatsApp/SMS/Email e BackgroundService para polling e despacho resiliente da fila de outbox.

## T-055 — Casos de Uso: Agendamento de Lembretes e Notificação de Orçamento [concluida]

- Refs: US-033, US-034, AC-062
- Arquivos: `src/Agendamento.Api/Application/Notifications/ScheduleAppointmentReminder/`, `src/Agendamento.Api/Application/Notifications/SendQuotationNotification/`
- Regra: Handlers para criar mensagens formatadas utilizando templates parametrizados do tenant.

## T-056 — Expor Endpoints Minimal API de Notificações e Histórico [concluida]

- Refs: US-036, AC-060, AC-064
- Arquivos: `src/Agendamento.Api/Features/Notifications/NotificationEndpoints.cs`, `src/Agendamento.Api/Program.cs`
- Regra: Endpoints `/api/v1/notifications` para consultar histórico de envios e disparar notificações manuais se necessário.

## T-057 — Testes de Integração do Outbox Processor e Formatação de Mensagens [concluida]

- Refs: AC-060, AC-061, AC-062, AC-063
- Arquivos: `tests/Agendamento.UnitTests/Notifications/NotificationDomainTests.cs`, `tests/Agendamento.IntegrationTests/Notifications/NotificationApiTests.cs`, `src/Agendamento.Api/Infrastructure/Notifications/MockCommunicationGateway.cs`
- Regra: Testar a renderização de templates, estratégia de retry em falhas e isolamento por tenant.
