# Tarefas: Notificações e Lembretes WhatsApp

## T-097 — Serviço Abstrato e Entidades de Notificação [pendente]

- Refs: US-051, US-052, AC-097, AC-098
- Arquivos: `src/Agendamento.Api/Application/Notifications/INotificationService.cs`, `src/Agendamento.Api/Domain/Notifications/NotificationRecord.cs`, `src/Agendamento.Api/Infrastructure/Notifications/ConsoleNotificationService.cs`
- Regra: Criar a abstração e a entidade de domínio `NotificationRecord` que será salva no banco como auditoria. Implementar o serviço apenas logando no Console por enquanto.

## T-098 — Mapeamento EF Core de Notificações [pendente]

- Refs: AC-098
- Arquivos: `src/Agendamento.Api/Infrastructure/Persistence/Configurations/NotificationRecordConfiguration.cs`, `src/Agendamento.Api/Infrastructure/Persistence/AgendamentoDbContext.cs`
- Regra: Mapear `NotificationRecord` e configurar `IsTenantOwned`.

## T-099 — Integração de Notificação no Agendamento [pendente]

- Refs: US-051, AC-099
- Arquivos: `src/Agendamento.Api/Application/Appointments/CreateAppointment/CreateAppointmentHandler.cs`
- Regra: Ao agendar (no Handler existente de Appointments), despachar uma notificação para o serviço avisando que o agendamento foi criado.

## T-100 — Testes de Integração de Notificação [pendente]

- Refs: AC-097, AC-098, AC-099
- Arquivos: `tests/Agendamento.IntegrationTests/Features/NotificationTests.cs`
- Regra: Testar a gravação no banco e a injeção do serviço.
