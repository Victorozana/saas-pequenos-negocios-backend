# Tarefas: Dashboard Executivo, Métricas e Relatórios do Prestador

## T-058 — Modelar DTOs e Consultas Agregadas de Métricas [pendente]

- Refs: US-037, US-038, US-039, AC-065, AC-066, AC-068
- Arquivos: `src/Agendamento.Api/Application/Dashboard/GetDashboardSummary/DashboardSummaryDto.cs`, `src/Agendamento.Api/Application/Dashboard/GetDashboardSummary/ConversionMetricsDto.cs`
- Regra: Estrutura de DTOs para KPIs financeiros, operacionais e estatísticas de conversão de orçamentos.

## T-059 — Caso de Uso: Consulta Resumida do Dashboard Executivo [pendente]

- Refs: US-037, US-038, AC-065, AC-066, AC-067, AC-068
- Arquivos: `src/Agendamento.Api/Application/Dashboard/GetDashboardSummary/GetDashboardSummaryHandler.cs`
- Regra: Handler otimizado via EF Core/LINQ agregando totais recebidos, a receber, ordens de serviço pendentes e taxa de conversão.

## T-060 — Caso de Uso: Agenda Sintetizada de Atendimentos [pendente]

- Refs: US-039, AC-065, AC-067
- Arquivos: `src/Agendamento.Api/Application/Dashboard/GetUpcomingSchedule/GetUpcomingScheduleHandler.cs`
- Regra: Consulta com ordenação cronológica dos agendamentos marcados para o dia e próximos 7 dias.

## T-061 — Caso de Uso e Gerador de Relatórios Executivos Consolidado [pendente]

- Refs: US-040, AC-069
- Arquivos: `src/Agendamento.Api/Application/Dashboard/ExportPerformanceReport/ExportPerformanceReportHandler.cs`, `src/Agendamento.Api/Infrastructure/Reports/CsvPerformanceReportGenerator.cs`
- Regra: Gerador de relatório em formato CSV contendo resumo de clientes, serviços e resultados financeiros do período.

## T-062 — Expor Endpoints Minimal API do Dashboard [pendente]

- Refs: US-037, US-038, US-039, US-040, AC-065
- Arquivos: `src/Agendamento.Api/Features/Dashboard/DashboardEndpoints.cs`, `src/Agendamento.Api/Program.cs`
- Regra: Endpoints `/api/v1/dashboard/summary`, `/api/v1/dashboard/schedule` e `/api/v1/dashboard/report` protegidos.

## T-063 — Testes de Integração de Agregadores e Performance do Dashboard [pendente]

- Refs: AC-065, AC-066, AC-067, AC-068, AC-069
- Arquivos: `tests/Agendamento.UnitTests/Dashboard/DashboardMetricTests.cs`, `tests/Agendamento.IntegrationTests/Dashboard/DashboardApiTests.cs`
- Regra: Testar a exatidão das agregantes de conversão, cálculo de receitas líquidas e velocidade de resposta da consulta.
