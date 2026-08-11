# Tarefas: Gestão de Orçamentos e Serviços

## T-088 — Entidades de Domínio para Orçamentos e Serviços [pendente]

- Refs: US-045, US-046, AC-090, AC-092
- Arquivos: `src/Agendamento.Api/Domain/Quotations/Quotation.cs`, `src/Agendamento.Api/Domain/Quotations/QuotationStatus.cs`, `src/Agendamento.Api/Domain/Quotations/QuotationItem.cs`, `src/Agendamento.Api/Domain/Quotations/DepositInfo.cs`, `src/Agendamento.Api/Domain/Quotations/DepositType.cs`
- Regra: Criar entidade de domínio `Quotation` associada ao `CustomerId` e `TenantId` com lista de itens (`QuotationItem`). Adicionar `QuotationStatus` (`Pendente`, `Aprovado`, `Rejeitado`).

## T-089 — Mapeamento EF Core para Orçamentos [pendente]

- Refs: US-045, AC-092
- Arquivos: `src/Agendamento.Api/Infrastructure/Persistence/Configurations/QuotationConfiguration.cs`, `src/Agendamento.Api/Infrastructure/Persistence/Configurations/QuotationItemConfiguration.cs`, `src/Agendamento.Api/Infrastructure/Persistence/AgendamentoDbContext.cs`
- Regra: Mapear as entidades e configurar o `IsTenantOwned`.

## T-090 — Endpoints de Gestão de Orçamentos [pendente]

- Refs: US-045, US-046, AC-090, AC-092, AC-093
- Arquivos: `src/Agendamento.Api/Features/Quotations/QuotationEndpoints.cs`, `src/Agendamento.Api/Program.cs`, `src/Agendamento.Api/Application/Quotations/GetQuotations/GetQuotationByIdHandler.cs`, `src/Agendamento.Api/Application/Quotations/GetQuotations/GetQuotationsHandler.cs`, `src/Agendamento.Api/Application/Quotations/GetQuotations/GetQuotationsQuery.cs`, `src/Agendamento.Api/Application/Quotations/UpdateQuotationStatus/UpdateQuotationStatusHandler.cs`
- Regra: Criar endpoints CRUD (Create, UpdateStatus, GetById, GetAll) usando Minimal APIs e a autorização `RequirePermission`. Reutilizar os Handlers existentes ou ajustá-los. *Nota: Os arquivos órfãos de Quotations identificados no audit serão integrados aqui.*

## T-091 — Caso de Uso: Transformar Orçamento em O.S. [pendente]

- Refs: US-047, AC-091, AC-093
- Arquivos: `src/Agendamento.Api/Application/WorkOrders/ConvertQuotationToWorkOrder/ConvertQuotationToWorkOrderHandler.cs`, `src/Agendamento.Api/Application/WorkOrders/ConvertQuotationToWorkOrder/ConvertQuotationToWorkOrderCommand.cs`, `src/Agendamento.Api/Features/Quotations/QuotationEndpoints.cs`
- Regra: Ajustar o handler (atualmente órfão) para validar AC-091 e expor na rota `/api/v1/quotations/{id}/convert`.

## T-092 — Testes de Integração para Orçamentos [pendente]

- Refs: AC-090, AC-091, AC-092, AC-093
- Arquivos: `tests/Agendamento.IntegrationTests/Features/QuotationTests.cs`, `tests/Agendamento.UnitTests/Quotations/QuotationDomainTests.cs`
- Regra: Testar validações de tenant, soma de itens, e transições de status válidas.
