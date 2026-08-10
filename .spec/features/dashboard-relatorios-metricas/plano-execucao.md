# Plano de execução — dashboard-relatorios-metricas

> gerado por `onp-spec plano` em 2026-08-10 23:29 — NÃO edite à mão;
> mudou tasks.md ou a config? Regenere: `onp-spec plano dashboard-relatorios-metricas`

## Resumo — o que vai acontecer

- **6 tarefa(s) pendente(s)**: 6 em 6 faixa(s) paralela(s) + 0 sequencial(is)
- **1 faixa = 1 worktree + 1 branch + 1 janela de contexto limpa** — faixas não compartilham nenhum arquivo entre si
- prefere outra seleção ou uma após a outra? Regenere com `onp-spec plano dashboard-relatorios-metricas --paralelizar T-xxx,T-yyy` ou `--sequencial`
- tudo acontece na branch de trabalho `spec/dashboard-relatorios-metricas`; levar para a main é decisão sua

## Faixas e ondas

### Onda 1 — faixa-1 ∥ faixa-2 ∥ faixa-3

#### faixa-1 — branch `spec/dashboard-relatorios-metricas-faixa-1` — worktree `../onp-worktrees/agendamento-dashboard-relatorios-metricas-faixa-1`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-058 | Modelar DTOs e Consultas Agregadas de Métricas | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Application/Dashboard/GetDashboardSummary/DashboardSummaryDto.cs`, `src/Agendamento.Api/Application/Dashboard/GetDashboardSummary/ConversionMetricsDto.cs` |

#### faixa-2 — branch `spec/dashboard-relatorios-metricas-faixa-2` — worktree `../onp-worktrees/agendamento-dashboard-relatorios-metricas-faixa-2`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-059 | Caso de Uso: Consulta Resumida do Dashboard Executivo | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Application/Dashboard/GetDashboardSummary/GetDashboardSummaryHandler.cs` |

#### faixa-3 — branch `spec/dashboard-relatorios-metricas-faixa-3` — worktree `../onp-worktrees/agendamento-dashboard-relatorios-metricas-faixa-3`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-060 | Caso de Uso: Agenda Sintetizada de Atendimentos | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Application/Dashboard/GetUpcomingSchedule/GetUpcomingScheduleHandler.cs` |

### Onda 2 — faixa-4 ∥ faixa-5 ∥ faixa-6

#### faixa-4 — branch `spec/dashboard-relatorios-metricas-faixa-4` — worktree `../onp-worktrees/agendamento-dashboard-relatorios-metricas-faixa-4`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-061 | Caso de Uso e Gerador de Relatórios Executivos Consolidado | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Application/Dashboard/ExportPerformanceReport/ExportPerformanceReportHandler.cs`, `src/Agendamento.Api/Infrastructure/Reports/CsvPerformanceReportGenerator.cs` |

#### faixa-5 — branch `spec/dashboard-relatorios-metricas-faixa-5` — worktree `../onp-worktrees/agendamento-dashboard-relatorios-metricas-faixa-5`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-062 | Expor Endpoints Minimal API do Dashboard | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Features/Dashboard/DashboardEndpoints.cs`, `src/Agendamento.Api/Program.cs` |

#### faixa-6 — branch `spec/dashboard-relatorios-metricas-faixa-6` — worktree `../onp-worktrees/agendamento-dashboard-relatorios-metricas-faixa-6`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-063 | Testes de Integração de Agregadores e Performance do Dashboard | `gpt-5.6-terra` | medium | `tests/Agendamento.UnitTests/Dashboard/DashboardMetricTests.cs`, `tests/Agendamento.IntegrationTests/Dashboard/DashboardApiTests.cs` |

## Gestão de branches e commits

1. branch de trabalho `spec/dashboard-relatorios-metricas` criada do ponto atual (se ainda não existir)
2. cada faixa nasce dela como branch própria e roda no seu worktree — **1 tarefa = 1 commit** (`T-xxx feature: título`)
3. terminou a onda → merge `--no-ff` de cada faixa de volta, na ordem; conflito interrompe a faixa e pede resolução humana
4. faixa mesclada → worktree removido, branch apagada, tarefa marcada `[concluida]` no tasks.md
5. gate final na branch de trabalho: `onp-spec verify dashboard-relatorios-metricas` + `onp-spec audit --ci` — **exit 0 ou não está pronto**

## Como executar

### ▶ Execução — Codex headless (codex exec)

```bash
bash .spec/features/dashboard-relatorios-metricas/executar-tarefas.sh
```

Cada faixa roda `codex exec` com **janela de contexto limpa**, no seu worktree, com
`--model` e `model_reasoning_effort` já definidos por tarefa e sandbox `workspace-write`. Os prompts exatos estão
embutidos no script — quer rodar uma faixa na mão, é só copiá-los de lá.
Logs: `../onp-worktrees/agendamento-dashboard-relatorios-metricas-logs/`.

**Confirmação de custos — antes de executar**: os modelos e esforços por
tarefa estão nas tabelas acima; o agente CONFIRMA com o usuário se estão
dentro da licença/cota dele (modelo forte + esforço alto torra tokens).
Para gastar menos: `onp-spec plano dashboard-relatorios-metricas --modelo gpt-5.6-luna --esforco baixo`
(tudo) ou por tarefa `onp-spec tarefa dashboard-relatorios-metricas T-xxx --modelo <m> --esforco <nível>` — e regenere o plano.

### 📣 Acompanhamento — tabela + resumo no chat (a cada 1 min)

O script roda em **background**: o agente AVISA o usuário antes de iniciar e,
enquanto roda, posta no chat a cada ~1 minuto a **tabela de andamento** (qual
tarefa está rodando, qual não está, o que concluiu/falhou) junto com o
**resumo geral de andamento** (escrito por IA; sem IA, o motor resume). Ao
final, o usuário recebe o resumo completo da execução. A qualquer momento:

```bash
onp-spec resumo dashboard-relatorios-metricas --tabela   # a tabela de andamento
onp-spec resumo dashboard-relatorios-metricas            # o resumo em texto
```

