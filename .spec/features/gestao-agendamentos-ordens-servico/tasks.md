# Tarefas: Gestão de Agendamentos e Ordens de Serviço (OS)

## T-038 — Modelar Entidades de Ordem de Serviço e Agendamentos [pendente]

- Refs: US-025, US-026, US-027, US-028, AC-050, AC-051, AC-052
- Arquivos: `src/Agendamento.Api/Domain/WorkOrders/WorkOrder.cs`, `src/Agendamento.Api/Domain/WorkOrders/WorkOrderItem.cs`, `src/Agendamento.Api/Domain/WorkOrders/WorkOrderStatus.cs`, `src/Agendamento.Api/Domain/Appointments/Appointment.cs`
- Regra: Entidades implementando `ITenantOwned`. Regras de conversão a partir de `Quotation` e vinculo de agendamento com data/hora e responsável.

## T-039 — Configurar Persistência EF Core e Migrations de OS e Agendamentos [pendente]

- Refs: US-025, US-026, AC-050
- Arquivos: `src/Agendamento.Api/Infrastructure/Persistence/AgendamentoDbContext.cs`, `src/Agendamento.Api/Infrastructure/Persistence/Configurations/WorkOrderConfiguration.cs`, `src/Agendamento.Api/Infrastructure/Persistence/Configurations/AppointmentConfiguration.cs`
- Regra: Mapeamento de tabelas `WorkOrders`, `WorkOrderItems` e `Appointments` com filtros globais de `TenantId` e índices de busca.

## T-040 — Caso de Uso: Conversão de Orçamento em Ordem de Serviço [pendente]

- Refs: US-025, AC-051, AC-052
- Arquivos: `src/Agendamento.Api/Application/WorkOrders/ConvertQuotationToWorkOrder/`
- Regra: Handler para validar status `Accepted` do orçamento, criar a nova OS copiando os itens e marcar o orçamento como vinculado.

## T-041 — Casos de Uso de Agendamentos e Gestão de Agenda [pendente]

- Refs: US-026, US-028, AC-053
- Arquivos: `src/Agendamento.Api/Application/Appointments/CreateAppointment/`, `src/Agendamento.Api/Application/Appointments/GetAppointments/`, `src/Agendamento.Api/Application/Appointments/UpdateAppointment/`
- Regra: Handlers para criar agendamentos (visita/medição/instalacao), listar por período/técnico e atualizar horários/status.

## T-042 — Caso de Uso: Atualização de Status e Progresso da OS [pendente]

- Refs: US-027, AC-054
- Arquivos: `src/Agendamento.Api/Application/WorkOrders/UpdateWorkOrderStatus/`, `src/Agendamento.Api/Application/WorkOrders/GetWorkOrderById/`
- Regra: Atualizar status (`Scheduled`, `InProgress`, `Completed`, `Cancelled`) com registro de log de transição.

## T-043 — Expor Endpoints Minimal API de OS e Agendamentos [pendente]

- Refs: US-025, US-026, US-027, US-028, AC-050
- Arquivos: `src/Agendamento.Api/Features/WorkOrders/WorkOrderEndpoints.cs`, `src/Agendamento.Api/Features/Appointments/AppointmentEndpoints.cs`, `src/Agendamento.Api/Program.cs`
- Regra: Endpoints `/api/v1/work-orders` e `/api/v1/appointments` protegidos por autenticação e com metadados OpenAPI.

## T-044 — Testes de Integração, Isolamento RLS e Validação de Conflito de Agenda [pendente]

- Refs: AC-050, AC-051, AC-053, AC-054
- Arquivos: `tests/Agendamento.UnitTests/WorkOrders/WorkOrderDomainTests.cs`, `tests/Agendamento.IntegrationTests/WorkOrders/WorkOrderApiTests.cs`
- Regra: Testes cobrindo conversão de orçamento, isolamento por tenant e detecção de choque de horários na agenda do técnico.
