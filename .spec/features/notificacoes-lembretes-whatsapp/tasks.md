# Tarefas: Notificações e Lembretes WhatsApp

## T-097 — Serviço Abstrato e Entidades de Notificação [concluida]

- Refs: US-051, US-052, AC-097, AC-098
- Arquivos: `src/Agendamento.Api/Infrastructure/Notifications/ICommunicationGateway.cs`, `src/Agendamento.Api/Domain/Notifications/NotificationMessage.cs`, `src/Agendamento.Api/Domain/Notifications/NotificationChannel.cs`, `src/Agendamento.Api/Domain/Notifications/NotificationStatus.cs`, `src/Agendamento.Api/Domain/Notifications/NotificationTemplate.cs`, `src/Agendamento.Api/Infrastructure/Notifications/MockCommunicationGateway.cs`, `src/Agendamento.Api/Infrastructure/Notifications/NotificationOutboxProcessor.cs`
- Regra: Entidades de domínio `NotificationMessage` que será salva no banco. Serviço usando `ICommunicationGateway`.

## T-098 — Mapeamento EF Core de Notificações [concluida]

- Refs: AC-098
- Arquivos: `src/Agendamento.Api/Infrastructure/Persistence/Configurations/NotificationMessageConfiguration.cs`, `src/Agendamento.Api/Infrastructure/Persistence/AgendamentoDbContext.cs`
- Regra: Mapear `NotificationMessage` e configurar `IsTenantOwned`.

## T-099 — Integração de Notificação no Agendamento [concluida]

- Refs: US-051, AC-099
- Arquivos: `src/Agendamento.Api/Application/Appointments/CreateAppointment/CreateAppointmentHandler.cs`
- Regra: Ao agendar (no Handler existente de Appointments), despachar uma notificação para o serviço avisando que o agendamento foi criado.

## T-100 — Testes de Integração de Notificação [concluida]

- Refs: AC-097, AC-098, AC-099
- Arquivos: `tests/Agendamento.IntegrationTests/Features/NotificationTests.cs`
- Regra: Testar a gravação no banco e a injeção do serviço.
