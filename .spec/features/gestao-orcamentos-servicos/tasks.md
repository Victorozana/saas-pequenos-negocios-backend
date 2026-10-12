# Tarefas: Gestão de Orçamentos e Serviços

## T-088 — Entidades de Domínio para Orçamentos e Serviços [pendente]

- Refs: US-045, US-046, AC-090, AC-092
- Arquivos: src/Agendamento.Api/Domain/Quotations/Quotation.cs, src/Agendamento.Api/Domain/Quotations/QuotationStatus.cs, src/Agendamento.Api/Domain/Quotations/QuotationItem.cs, src/Agendamento.Api/Domain/Quotations/DepositInfo.cs, src/Agendamento.Api/Domain/Quotations/DepositType.cs
- Regra: Criar entidade de domínio `Quotation` associada ao `CustomerId` e `TenantId` com lista de itens (`QuotationItem`). Adicionar `QuotationStatus` (`Pendente`, `Aprovado`, `Rejeitado`).

## T-089 — Mapeamento EF Core para Orçamentos [pendente]

- Refs: US-045, AC-092
- Arquivos: src/Agendamento.Api/Infrastructure/Persistence/Configurations/QuotationConfiguration.cs, src/Agendamento.Api/Infrastructure/Persistence/AgendamentoDbContext.cs
- Regra: Mapear as entidades e configurar o `IsTenantOwned`.

## T-090 — Endpoints de Gestão de Orçamentos [pendente]

- Refs: US-045, US-046, AC-090, AC-092, AC-093
- Arquivos: src/Agendamento.Api/Features/Quotations/QuotationEndpoints.cs, src/Agendamento.Api/Program.cs, src/Agendamento.Api/Application/Quotations/GetQuotations/GetQuotationByIdHandler.cs, src/Agendamento.Api/Application/Quotations/GetQuotations/GetQuotationsHandler.cs, src/Agendamento.Api/Application/Quotations/GetQuotations/GetQuotationsQuery.cs, src/Agendamento.Api/Application/Quotations/UpdateQuotationStatus/UpdateQuotationStatusHandler.cs
- Regra: Criar endpoints CRUD (Create, UpdateStatus, GetById, GetAll) usando Minimal APIs e a autorização `RequirePermission`. Reutilizar os Handlers existentes ou ajustá-los. *Nota: Os arquivos órfãos de Quotations identificados no audit serão integrados aqui.*

## T-091 — Caso de Uso: Transformar Orçamento em O.S. [pendente]

- Refs: US-047, AC-091, AC-093
- Arquivos: src/Agendamento.Api/Application/WorkOrders/ConvertQuotationToWorkOrder/ConvertQuotationToWorkOrderHandler.cs, src/Agendamento.Api/Application/WorkOrders/ConvertQuotationToWorkOrder/ConvertQuotationToWorkOrderCommand.cs, src/Agendamento.Api/Features/Quotations/QuotationEndpoints.cs
- Regra: Ajustar o handler (atualmente órfão) para validar AC-091 e expor na rota `/api/v1/quotations/{id}/convert`.

## T-092 — Testes de Integração para Orçamentos [pendente]

- Refs: AC-090, AC-091, AC-092, AC-093
- Arquivos: tests/Agendamento.IntegrationTests/Features/QuotationTests.cs, tests/Agendamento.UnitTests/Quotations/QuotationDomainTests.cs
- Regra: Testar validações de tenant, soma de itens, e transições de status válidas.

## T-031 — Modelar Entidades do Catálogo de Serviços e Orçamentos [pendente]

- Refs: US-020, US-021, US-022, AC-045, AC-046
- Arquivos: src/Agendamento.Api/Domain/Services/ServiceItem.cs, src/Agendamento.Api/Domain/Quotations/Quotation.cs, src/Agendamento.Api/Domain/Quotations/QuotationItem.cs, src/Agendamento.Api/Domain/Quotations/QuotationStatus.cs, src/Agendamento.Api/Domain/Quotations/DepositInfo.cs, src/Agendamento.Api/Domain/Quotations/DepositType.cs
- Regra: Implementar `ITenantOwned` em `ServiceItem` e `Quotation`. Criar regras de cálculo de totais e sinal/entrada (porcentagem ou valor fixo, saldo restante).

## T-032 — Mapear Persistência EF Core e Migrations [pendente]

- Refs: US-020, US-021, AC-045
- Arquivos: src/Agendamento.Api/Infrastructure/Persistence/AgendamentoDbContext.cs, src/Agendamento.Api/Infrastructure/Persistence/Configurations/ServiceItemConfiguration.cs, src/Agendamento.Api/Infrastructure/Persistence/Configurations/QuotationConfiguration.cs
- Regra: Adicionar `DbSet<ServiceItem>` e `DbSet<Quotation>`, configurar `HasQueryFilter` por `TenantId`, relacionamentos com `Customer` e tabelas/owned types de itens e sinal.

## T-033 — Orquestrar Casos de Uso do Catálogo de Serviços/Produtos [pendente]

- Refs: US-020, AC-045
- Arquivos: src/Agendamento.Api/Application/Services/CreateService/CreateServiceCommand.cs, src/Agendamento.Api/Application/Services/CreateService/CreateServiceHandler.cs, src/Agendamento.Api/Application/Services/GetServices/GetServiceByIdHandler.cs, src/Agendamento.Api/Application/Services/GetServices/GetServicesHandler.cs, src/Agendamento.Api/Application/Services/GetServices/GetServicesQuery.cs, src/Agendamento.Api/Application/Services/UpdateService/UpdateServiceCommand.cs, src/Agendamento.Api/Application/Services/UpdateService/UpdateServiceHandler.cs
- Regra: Handlers para cadastro, listagem paginada/com busca e atualização do catálogo de serviços/produtos do tenant.

## T-034 — Orquestrar Casos de Uso da Gestão de Orçamentos [pendente]

- Refs: US-021, US-022, US-024, AC-045, AC-046, AC-048
- Arquivos: src/Agendamento.Api/Application/Quotations/CreateQuotation/CreateQuotationCommand.cs, src/Agendamento.Api/Application/Quotations/CreateQuotation/CreateQuotationHandler.cs, src/Agendamento.Api/Application/Quotations/GetQuotations/GetQuotationByIdHandler.cs, src/Agendamento.Api/Application/Quotations/GetQuotations/GetQuotationsHandler.cs, src/Agendamento.Api/Application/Quotations/GetQuotations/GetQuotationsQuery.cs, src/Agendamento.Api/Application/Quotations/UpdateQuotationStatus/UpdateQuotationStatusHandler.cs
- Regra: Handlers para criação de orçamento com itens e regras de Sinal/Entrada, busca com detalhes do cliente e itens, e transição de status.

## T-035 — Implementar Gerador de Exportação de Orçamento em PDF [pendente]

- Refs: US-023, AC-047
- Arquivos: src/Agendamento.Api/Application/Quotations/ExportPdf/IQuotationPdfGenerator.cs, src/Agendamento.Api/Infrastructure/Pdf/QuestPdfQuotationGenerator.cs
- Regra: Gerar arquivo PDF profissional contendo dados da empresa/tenant, cliente, tabela de itens, cálculo de sinal/entrada e termos de pagamento. Retorno em Stream/byte array sem links públicos.

## T-036 — Expor Endpoints Minimal API de Serviços e Orçamentos [pendente]

- Refs: US-020, US-021, US-022, US-023, US-024, AC-045, AC-047, AC-049
- Arquivos: src/Agendamento.Api/Features/Services/ServiceEndpoints.cs, src/Agendamento.Api/Features/Quotations/QuotationEndpoints.cs, src/Agendamento.Api/Program.cs
- Regra: Endpoints `/api/v1/services` e `/api/v1/quotations` (incluindo GET `/api/v1/quotations/{id}/pdf`) com `RequireAuthorization()` e Swagger metadata.

## T-037 — Testes de Integração, Isolamento Multi-Tenant e Exportação PDF [pendente]

- Refs: AC-045, AC-046, AC-047, AC-048, AC-049
- Arquivos: tests/Agendamento.UnitTests/Quotations/QuotationDomainTests.cs, tests/Agendamento.IntegrationTests/Quotations/QuotationApiTests.cs
- Regra: Validar isolamento RLS entre tenants, cálculos de total/sinal, transições de status e integridade da geração do PDF (`application/pdf`).

## T-103 — Implementar e provar edição de orçamento pendente [concluida]

- Refs: US-045, AC-206
- Arquivos: src/Agendamento.Api/Features/Quotations/QuotationEndpoints.cs, src/Agendamento.Api/Domain/Quotations/Quotation.cs, src/Agendamento.Api/Application/Quotations/UpdateQuotation/UpdateQuotationCommand.cs, src/Agendamento.Api/Application/Quotations/UpdateQuotation/UpdateQuotationHandler.cs, src/Agendamento.Api/Application/DependencyInjection.cs, tests/Agendamento.IntegrationTests/Quotations/QuotationEditHttpTests.cs, tests/Agendamento.IntegrationTests/Quotations/QuotationEditPostgreSqlTests.cs
- Notas: PUT edita conteúdo completo apenas enquanto Pending, valida antes de modificar, substitui itens e recalcula totais/sinal. PostgreSQL usa transação e bloqueio de linha compartilhado com aprovação. Seis testes focados passaram em 12/10/2026, incluindo persistência PostgreSQL em base exclusiva, orçamento enviado, isolamento, estados finais imutáveis e edição concorrente com aprovação; prova granular da feature deve ser regenerada pelo verify.

## Revisão de provas — 12/10/2026

- T-031 a T-037 recuperadas do commit f326f63, inicialmente pendentes para revisão de cobertura completa.
- QuotationItem não possui arquivo de configuração separado; o mapeamento existente está em QuotationConfiguration/AgendamentoDbContext.
- T-088 a T-092 permanecem pendentes até revisão de abrangência: o teste AC-093 cobre orçamento convertido, mas não todas as combinações de aprovado/rejeitado; a aprovação repetida também é idempotente no fluxo financeiro.
- Edição de orçamento pendente está em T-103, com testes HTTP e PostgreSQL anotados para AC-206; confirmação do usuário não substitui a prova gerada pelo verify.
