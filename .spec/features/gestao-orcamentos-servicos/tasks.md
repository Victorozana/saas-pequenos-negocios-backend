# Tarefas: Gestão de Orçamentos e Serviços

## T-031 — Modelar Entidades do Catálogo de Serviços e Orçamentos [concluida]

- Refs: US-020, US-021, US-022, AC-045, AC-046
- Arquivos: `src/Agendamento.Api/Domain/Services/ServiceItem.cs`, `src/Agendamento.Api/Domain/Quotations/Quotation.cs`, `src/Agendamento.Api/Domain/Quotations/QuotationItem.cs`, `src/Agendamento.Api/Domain/Quotations/QuotationStatus.cs`, `src/Agendamento.Api/Domain/Quotations/DepositInfo.cs`, `src/Agendamento.Api/Domain/Quotations/DepositType.cs`
- Regra: Implementar `ITenantOwned` em `ServiceItem` e `Quotation`. Criar regras de cálculo de totais e sinal/entrada (porcentagem ou valor fixo, saldo restante).

## T-032 — Mapear Persistência EF Core e Migrations [concluida]

- Refs: US-020, US-021, AC-045
- Arquivos: `src/Agendamento.Api/Infrastructure/Persistence/AgendamentoDbContext.cs`, `src/Agendamento.Api/Infrastructure/Persistence/Configurations/ServiceItemConfiguration.cs`, `src/Agendamento.Api/Infrastructure/Persistence/Configurations/QuotationConfiguration.cs`
- Regra: Adicionar `DbSet<ServiceItem>` e `DbSet<Quotation>`, configurar `HasQueryFilter` por `TenantId`, relacionamentos com `Customer` e tabelas/owned types de itens e sinal.

## T-033 — Orquestrar Casos de Uso do Catálogo de Serviços/Produtos [concluida]

- Refs: US-020, AC-045
- Arquivos: `src/Agendamento.Api/Application/Services/CreateService/`, `src/Agendamento.Api/Application/Services/GetServices/`, `src/Agendamento.Api/Application/Services/UpdateService/`
- Regra: Handlers para cadastro, listagem paginada/com busca e atualização do catálogo de serviços/produtos do tenant.

## T-034 — Orquestrar Casos de Uso da Gestão de Orçamentos [concluida]

- Refs: US-021, US-022, US-024, AC-045, AC-046, AC-048
- Arquivos: `src/Agendamento.Api/Application/Quotations/CreateQuotation/`, `src/Agendamento.Api/Application/Quotations/GetQuotations/`, `src/Agendamento.Api/Application/Quotations/UpdateQuotationStatus/`
- Regra: Handlers para criação de orçamento com itens e regras de Sinal/Entrada, busca com detalhes do cliente e itens, e transição de status.

## T-035 — Implementar Gerador de Exportação de Orçamento em PDF [concluida]

- Refs: US-023, AC-047
- Arquivos: `src/Agendamento.Api/Application/Quotations/ExportPdf/IQuotationPdfGenerator.cs`, `src/Agendamento.Api/Infrastructure/Pdf/QuestPdfQuotationGenerator.cs`
- Regra: Gerar arquivo PDF profissional contendo dados da empresa/tenant, cliente, tabela de itens, cálculo de sinal/entrada e termos de pagamento. Retorno em Stream/byte array sem links públicos.

## T-036 — Expor Endpoints Minimal API de Serviços e Orçamentos [concluida]

- Refs: US-020, US-021, US-022, US-023, US-024, AC-045, AC-047, AC-049
- Arquivos: `src/Agendamento.Api/Features/Services/ServiceEndpoints.cs`, `src/Agendamento.Api/Features/Quotations/QuotationEndpoints.cs`, `src/Agendamento.Api/Program.cs`
- Regra: Endpoints `/api/v1/services` e `/api/v1/quotations` (incluindo GET `/api/v1/quotations/{id}/pdf`) com `RequireAuthorization()` e Swagger metadata.

## T-037 — Testes de Integração, Isolamento Multi-Tenant e Exportação PDF [concluida]

- Refs: AC-045, AC-046, AC-047, AC-048, AC-049
- Arquivos: `tests/Agendamento.UnitTests/Quotations/QuotationDomainTests.cs`, `tests/Agendamento.IntegrationTests/Quotations/QuotationApiTests.cs`
- Regra: Validar isolamento RLS entre tenants, cálculos de total/sinal, transições de status e integridade da geração do PDF (`application/pdf`).
