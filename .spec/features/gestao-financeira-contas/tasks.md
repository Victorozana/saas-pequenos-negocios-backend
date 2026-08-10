# Tarefas: Gestão Financeira e Contas a Receber / Pagar

## T-045 — Modelar Entidades Financeiras (FinancialTransaction, Receivable, Payable) [concluida]

- Refs: US-029, US-030, US-031, AC-055, AC-057, AC-058
- Arquivos: `src/Agendamento.Api/Domain/Financial/ReceivableTitle.cs`, `src/Agendamento.Api/Domain/Financial/PayableTitle.cs`, `src/Agendamento.Api/Domain/Financial/PaymentEntry.cs`, `src/Agendamento.Api/Domain/Financial/TransactionStatus.cs`, `src/Agendamento.Api/Domain/Financial/PaymentMethod.cs`
- Regra: Entidades com `ITenantOwned`. Lógica de cálculo de saldos, baixa parcial/total de títulos e classificação de atrasados (`Overdue`).

## T-046 — Configurar Persistência EF Core e Migrations Financeiras [concluida]

- Refs: US-029, US-031, AC-055
- Arquivos: `src/Agendamento.Api/Infrastructure/Persistence/AgendamentoDbContext.cs`, `src/Agendamento.Api/Infrastructure/Persistence/Configurations/ReceivableTitleConfiguration.cs`, `src/Agendamento.Api/Infrastructure/Persistence/Configurations/PayableTitleConfiguration.cs`
- Regra: Mapear tabelas `ReceivableTitles`, `PayableTitles` e `PaymentEntries` com RLS por `TenantId` e índices por data de vencimento/status.

## T-047 — Caso de Uso: Lançamento Automático de Títulos via Orçamento/OS [concluida]

- Refs: US-029, AC-056
- Arquivos: `src/Agendamento.Api/Application/Financial/GenerateReceivablesFromQuotation/`
- Regra: Handler disparado na aprovação do orçamento para gerar o título do Sinal/Entrada e as parcelas restantes.

## T-048 — Caso de Uso: Registro de Liquidação / Baixa de Títulos [concluida]

- Refs: US-030, AC-057
- Arquivos: `src/Agendamento.Api/Application/Financial/RegisterPayment/`
- Regra: Handler para registrar pagamento (Pix, Cartão, Dinheiro), atualizar saldo devedor do título e transicionar status para `PartiallyPaid` ou `Paid`.

## T-049 — Casos de Uso: Gestão de Contas a Pagar e Extrato Financeiro [concluida]

- Refs: US-031, US-032, AC-058, AC-059
- Arquivos: `src/Agendamento.Api/Application/Financial/CreatePayable/`, `src/Agendamento.Api/Application/Financial/GetFinancialStatement/`
- Regra: Handlers para criar despesas operacionais e calcular extrato financeiro (entradas, saídas, saldo previsto e saldo realizado).

## T-050 — Expor Endpoints Minimal API Financeiros [concluida]

- Refs: US-029, US-030, US-031, US-032, AC-055, AC-059
- Arquivos: `src/Agendamento.Api/Features/Financial/FinancialEndpoints.cs`, `src/Agendamento.Api/Program.cs`
- Regra: Endpoints `/api/v1/financial/receivables`, `/api/v1/financial/payables` e `/api/v1/financial/statement` protegidos com Swagger.

## T-051 — Testes de Integração Financeira, Baixas Parciais e Isolamento RLS [concluida]

- Refs: AC-055, AC-056, AC-057, AC-058, AC-059
- Arquivos: `tests/Agendamento.UnitTests/Financial/FinancialDomainTests.cs`, `tests/Agendamento.IntegrationTests/Financial/FinancialApiTests.cs`, `tests/Agendamento.IntegrationTests/Financial/FinancialIsolationTests.cs`
- Regra: Validação unitária e de integração dos cálculos de saldo, geração de entradas, títulos em atraso e isolamento de tenant.
