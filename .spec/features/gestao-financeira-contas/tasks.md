# Tarefas: Gestão Financeira e Contas

## T-093 — Entidades de Domínio para Financeiro [pendente]

- Refs: US-048, US-049, AC-094, AC-095, AC-096
- Arquivos: `src/Agendamento.Api/Domain/Financial/Transaction.cs`, `src/Agendamento.Api/Domain/Financial/TransactionType.cs`, `src/Agendamento.Api/Domain/Financial/TransactionStatus.cs`
- Regra: Criar entidade de domínio `Transaction` (Conta a pagar/receber) com tipo, status, valor original, valor pago, e vinculo com `TenantId`, `CustomerId` e possivelmente `WorkOrderId`.

## T-094 — Mapeamento EF Core para Financeiro [pendente]

- Refs: US-048, AC-096
- Arquivos: `src/Agendamento.Api/Infrastructure/Persistence/Configurations/TransactionConfiguration.cs`, `src/Agendamento.Api/Infrastructure/Persistence/AgendamentoDbContext.cs`
- Regra: Mapear as entidades e configurar o `IsTenantOwned`.

## T-095 — Caso de Uso e Endpoints de Financeiro [pendente]

- Refs: US-048, US-049, US-050, AC-094, AC-095, AC-096
- Arquivos: `src/Agendamento.Api/Features/Financial/FinancialEndpoints.cs`, `src/Agendamento.Api/Program.cs`, `src/Agendamento.Api/Application/Financial/PayTransaction/PayTransactionHandler.cs`, `src/Agendamento.Api/Application/Financial/CreateTransaction/CreateTransactionHandler.cs`
- Regra: Criar endpoints Minimal APIs e comandos para criar conta e registrar pagamento parcial/total.

## T-096 — Testes de Integração para Financeiro [pendente]

- Refs: AC-094, AC-095, AC-096
- Arquivos: `tests/Agendamento.IntegrationTests/Features/FinancialTests.cs`, `tests/Agendamento.UnitTests/Financial/FinancialDomainTests.cs`
- Regra: Testar isolamento por tenant, baixa de pagamentos com atualização de status, e vinculações.
