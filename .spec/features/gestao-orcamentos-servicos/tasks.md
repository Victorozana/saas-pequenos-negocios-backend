# Tarefas: Gestão de Orçamentos e Serviços

## T-088 — Entidades de Domínio para Orçamentos e Serviços [concluida]
- Refs: US-045, US-046, AC-090, AC-092
- Arquivos: src/Agendamento.Api/Domain/Quotations/Quotation.cs, src/Agendamento.Api/Domain/Quotations/QuotationStatus.cs, src/Agendamento.Api/Domain/Quotations/QuotationItem.cs, src/Agendamento.Api/Domain/Quotations/DepositInfo.cs, src/Agendamento.Api/Domain/Quotations/DepositType.cs, src/Agendamento.Api/Application/Quotations/UpdateQuotationStatus/UpdateQuotationStatusHandler.cs, tests/Agendamento.UnitTests/Quotations/QuotationLifecycleTests.cs, tests/Agendamento.UnitTests/Quotations/QuotationStatusAtomicityTests.cs, tests/Agendamento.IntegrationTests/Quotations/QuotationLifecycleHttpTests.cs
- Notas: Revisão própria Draft/Pending, associação obrigatória, coleção, todas as transições e recusas sem mutação. Q-022 respondida: AC-093 documenta exceção idempotente de aprovação pública repetida, preservando fluxo financeiro e bloqueando rejeições repetidas. Provas e fechamento em .spec/revisao-t088.md; T-089 a T-092 e T-032 a T-037 não certificadas automaticamente.
- Regra: Criar entidade de domínio `Quotation` associada ao `CustomerId` e `TenantId` com lista de itens (`QuotationItem`). Adicionar `QuotationStatus` (`Pendente`, `Aprovado`, `Rejeitado`).

## T-089 — Mapeamento EF Core para Orçamentos [concluida]
- Refs: US-045, AC-092
- Arquivos: src/Agendamento.Api/Infrastructure/Persistence/Configurations/QuotationConfiguration.cs, src/Agendamento.Api/Infrastructure/Persistence/AgendamentoDbContext.cs, src/Agendamento.Api/Infrastructure/Persistence/TenantSaveChangesInterceptor.cs, src/Agendamento.Api/Infrastructure/Persistence/Migrations/20261013173339_EnforceQuotationCatalogTenantReference.cs, src/Agendamento.Api/Infrastructure/Persistence/Migrations/20261013173339_EnforceQuotationCatalogTenantReference.Designer.cs, src/Agendamento.Api/Infrastructure/Persistence/Migrations/AgendamentoDbContextModelSnapshot.cs, tests/Agendamento.IntegrationTests/Quotations/QuotationMappingTests.cs, tests/Agendamento.IntegrationTests/Quotations/QuotationPersistenceTests.cs, tests/Agendamento.IntegrationTests/Features/TeamFinancialHttpTests.cs, tests/Agendamento.UnitTests/Quotations/QuotationStatusAtomicityTests.cs
- Notas: Revisão própria do modelo efetivo, roundtrip integral, filtros e guardas de itens/sinal, integridade e RLS em PostgreSQL exclusivo. FK composta do catálogo corrige lacuna real; migration não aplicada ao banco de trabalho. T-032 inclui catálogo/migrations como escopo próprio e permanece pendente, assim como T-090 a T-092 e T-033 a T-037.
- Regra: Mapear as entidades e configurar o `IsTenantOwned`.

## T-090 — Endpoints de Gestão de Orçamentos [concluida]
- Refs: US-045, US-046, AC-090, AC-092, AC-093
- Arquivos: src/Agendamento.Api/Features/Quotations/QuotationEndpoints.cs, src/Agendamento.Api/Program.cs, src/Agendamento.Api/Application/DependencyInjection.cs, src/Agendamento.Api/Application/Quotations/QuotationInputValidation.cs, src/Agendamento.Api/Application/Quotations/CreateQuotation/CreateQuotationHandler.cs, src/Agendamento.Api/Application/Quotations/UpdateQuotation/UpdateQuotationHandler.cs, src/Agendamento.Api/Application/Quotations/GetQuotations/GetQuotationByIdHandler.cs, src/Agendamento.Api/Application/Quotations/GetQuotations/GetQuotationsHandler.cs, src/Agendamento.Api/Application/Quotations/GetQuotations/GetQuotationsQuery.cs, src/Agendamento.Api/Application/Quotations/UpdateQuotationStatus/UpdateQuotationStatusHandler.cs, tests/Agendamento.IntegrationTests/Quotations/QuotationEndpointsHttpTests.cs, tests/Agendamento.IntegrationTests/Quotations/QuotationEndpointAtomicityTests.cs
- Notas: Revisão própria HTTP/autorização/binding/listagem/detalhe/status e integração PUT/T-103. Limites/paginação, desempate, unicidade e validação antes de mutação; provas PostgreSQL restrito, corrida de criação e contratos. Não certifica T-034/T-036/T-091/T-092 nem concorrência integral de conversão. A nota de órfãos abaixo é histórica e não descreve diagnóstico atual.
- Regra: Criar endpoints CRUD (Create, UpdateStatus, GetById, GetAll) usando Minimal APIs e a autorização `RequirePermission`. Reutilizar os Handlers existentes ou ajustá-los. *Nota: Os arquivos órfãos de Quotations identificados no audit serão integrados aqui.*

## T-091 — Caso de Uso: Transformar Orçamento em O.S. [concluida]
- Refs: US-047, AC-091, AC-093
- Arquivos: src/Agendamento.Api/Application/WorkOrders/ConvertQuotationToWorkOrder/ConvertQuotationToWorkOrderHandler.cs, src/Agendamento.Api/Application/WorkOrders/ConvertQuotationToWorkOrder/ConvertQuotationToWorkOrderCommand.cs, src/Agendamento.Api/Features/Quotations/QuotationEndpoints.cs, src/Agendamento.Api/Features/WorkOrders/WorkOrderEndpoints.cs, src/Agendamento.Api/Domain/WorkOrders/WorkOrder.cs, src/Agendamento.Api/Domain/WorkOrders/WorkOrderItem.cs, src/Agendamento.Api/Infrastructure/Persistence/Configurations/WorkOrderConfiguration.cs, src/Agendamento.Api/Infrastructure/Persistence/TenantSaveChangesInterceptor.cs, src/Agendamento.Api/Infrastructure/Persistence/Migrations/20261013202849_EnforceWorkOrderConversionIntegrity.cs, src/Agendamento.Api/Infrastructure/Persistence/Migrations/20261013202849_EnforceWorkOrderConversionIntegrity.Designer.cs, src/Agendamento.Api/Infrastructure/Persistence/Migrations/AgendamentoDbContextModelSnapshot.cs, tests/Agendamento.UnitTests/Quotations/QuotationConversionAtomicityTests.cs, tests/Agendamento.IntegrationTests/Quotations/QuotationConversionHttpTests.cs, tests/Agendamento.IntegrationTests/Quotations/QuotationConversionConcurrencyTests.cs, tests/Agendamento.IntegrationTests/Quotations/QuotationConversionIntegrityTests.cs, tests/Agendamento.IntegrationTests/Quotations/QuotationConversionContractTests.cs, tests/Agendamento.IntegrationTests/Features/TeamFinancialHttpTests.cs, tests/Agendamento.IntegrationTests/Quotations/QuotationMappingTests.cs, tests/Agendamento.IntegrationTests/Quotations/QuotationPersistenceTests.cs
- Notas: Revisão própria em .spec/revisao-t091.md; 39 casos novos, PostgreSQL restrito exclusivo, concorrência de duas/cinco sessões e rollback após writes. 15 features motor0/record0,400PASS/zero falhas/6skips, audit --ci exit0/95 critérios/zero erros e avisos. Contratos200/workOrderId e201/id/Location preservados; nova migration não aplicada ao banco de trabalho. Não conclui T-092 ou T-032 a T-037.
- Regra: Revisar e provar a conversão AC-091 nas duas rotas existentes, com idempotência, cópia integral, início manual, isolamento, integridade, transação e concorrência reais. A nota histórica de handler órfão não descreve o registro atual.

## T-092 — Testes de Integração para Orçamentos [concluida]
- Refs: AC-090, AC-091, AC-092, AC-093
- Arquivos: tests/Agendamento.IntegrationTests/Features/QuotationTests.cs, tests/Agendamento.UnitTests/Quotations/QuotationDomainTests.cs, tests/Agendamento.IntegrationTests/Quotations/QuotationIntegrationWorkflowTests.cs
- Notas: Revisão própria da validade das fixtures e cobertura conjunta em .spec/matriz-t092.md e .spec/revisao-t092.md. Fixture usa guarda de itens, banco exclusivo por factory, UoW do mesmo contexto e leitura em contexto novo. Jornada HTTP InMemory/PostgreSQL e decisões finais rastreadas complementam as provas anteriores, sem encerrar T-032 a T-037.
- Regra: Testar validações de tenant, soma de itens, e transições de status válidas.

## T-031 — Modelar Entidades do Catálogo de Serviços e Orçamentos [concluida]
- Refs: US-020, US-021, US-022, AC-045, AC-046
- Arquivos: src/Agendamento.Api/Domain/Services/ServiceItem.cs, src/Agendamento.Api/Domain/Quotations/Quotation.cs, src/Agendamento.Api/Domain/Quotations/QuotationItem.cs, src/Agendamento.Api/Domain/Quotations/QuotationStatus.cs, src/Agendamento.Api/Domain/Quotations/DepositInfo.cs, src/Agendamento.Api/Domain/Quotations/DepositType.cs, src/Agendamento.Api/Domain/Quotations/QuotationMoney.cs, src/Agendamento.Api/Application/Quotations/CreateQuotation/CreateQuotationHandler.cs, src/Agendamento.Api/Application/Financial/GenerateReceivablesFromQuotation/GenerateReceivablesFromQuotationHandler.cs, src/Agendamento.Api/Application/WorkOrders/GetWorkOrderById/WorkOrderResponse.cs, tests/Agendamento.UnitTests/Quotations/QuotationModelValidationTests.cs, tests/Agendamento.IntegrationTests/Quotations/QuotationModelHttpTests.cs
- Notas: Revisão T-031 com cálculos, limites e atomicidade; design.md registra decisões confirmadas. T-088 e outras tarefas históricas permanecem pendentes.
- Regra: Implementar `ITenantOwned` em `ServiceItem` e `Quotation`. Criar regras de cálculo de totais e sinal/entrada (porcentagem ou valor fixo, saldo restante).

## T-032 — Mapear Persistência EF Core e Migrations [concluida]
- Refs: US-020, US-021, AC-045
- Arquivos: src/Agendamento.Api/Infrastructure/Persistence/AgendamentoDbContext.cs, src/Agendamento.Api/Infrastructure/Persistence/Configurations/ServiceItemConfiguration.cs, src/Agendamento.Api/Infrastructure/Persistence/Configurations/QuotationConfiguration.cs, src/Agendamento.Api/Domain/Services/ServiceItem.cs, tests/Agendamento.UnitTests/Services/ServiceCatalogValidationTests.cs, tests/Agendamento.IntegrationTests/Services/ServiceCatalogValidationTests.cs, tests/Agendamento.IntegrationTests/Services/ServiceCatalogMappingTests.cs, tests/Agendamento.IntegrationTests/Services/ServiceCatalogPersistenceTests.cs, tests/Agendamento.IntegrationTests/Quotations/QuotationAggregateGuardTests.cs
- Notas: Revisão própria concluída em .spec/revisao-t032.md e matriz .spec/matriz-t032.md:62casos novos, metadata efetiva, catálogo integral/Unicode/limites, guardas root/batch, RLS físico e upgrades. Validação Name200/Description1000/Unit50 antes de mutar; nenhuma migration nova.15features motor0/record0,476resultados/470PASS/zero falhas/6skips; audit de revisão --ci exit0/95critérios/zero erros e avisos. T-033 a T-037 permanecem pendentes de revisão própria.
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

## Fechamento da revisão T-031 — 12/10/2026

T-031 concluída após revisão das seis entidades, decisões financeiras confirmadas e provas de cálculos, validações, ausência de mutação, HTTP, PostgreSQL temporário/RLS e consumidores. Ver .spec/revisao-t031.md e design.md. As 15 features renovadas têm motor=0/record.exitCode=0, 266 resultados (260 PASS, seis skips antigos, zero falhas) por execução. T-088 e demais tarefas históricas continuam pendentes; não são certificadas por este fechamento.
