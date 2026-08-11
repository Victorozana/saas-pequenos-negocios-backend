# Plano de execução — gestao-orcamentos-servicos

> gerado por `onp-spec plano` em 2026-08-11 18:10 — NÃO edite à mão;
> mudou tasks.md ou a config? Regenere: `onp-spec plano gestao-orcamentos-servicos`

## Resumo — o que vai acontecer

- **5 tarefa(s) pendente(s)**: 5 em 4 faixa(s) paralela(s) + 0 sequencial(is)
- **1 faixa = 1 worktree + 1 branch + 1 janela de contexto limpa** — faixas não compartilham nenhum arquivo entre si
- prefere outra seleção ou uma após a outra? Regenere com `onp-spec plano gestao-orcamentos-servicos --paralelizar T-xxx,T-yyy` ou `--sequencial`
- tudo acontece na branch de trabalho `spec/gestao-orcamentos-servicos`; levar para a main é decisão sua

## Faixas e ondas

### Onda 1 — faixa-1 ∥ faixa-2 ∥ faixa-3

#### faixa-1 — branch `spec/gestao-orcamentos-servicos-faixa-1` — worktree `../onp-worktrees/agendamento-gestao-orcamentos-servicos-faixa-1`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-088 | Entidades de Domínio para Orçamentos e Serviços | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Domain/Quotations/Quotation.cs`, `src/Agendamento.Api/Domain/Quotations/QuotationStatus.cs`, `src/Agendamento.Api/Domain/Quotations/QuotationItem.cs` |

#### faixa-2 — branch `spec/gestao-orcamentos-servicos-faixa-2` — worktree `../onp-worktrees/agendamento-gestao-orcamentos-servicos-faixa-2`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-089 | Mapeamento EF Core para Orçamentos | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Infrastructure/Persistence/Configurations/QuotationConfiguration.cs`, `src/Agendamento.Api/Infrastructure/Persistence/Configurations/QuotationItemConfiguration.cs`, `src/Agendamento.Api/Infrastructure/Persistence/AgendamentoDbContext.cs` |

#### faixa-3 — branch `spec/gestao-orcamentos-servicos-faixa-3` — worktree `../onp-worktrees/agendamento-gestao-orcamentos-servicos-faixa-3`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-090 | Endpoints de Gestão de Orçamentos | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Features/Quotations/QuotationEndpoints.cs`, `src/Agendamento.Api/Program.cs` |
| T-091 | Caso de Uso: Transformar Orçamento em O.S. | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Application/WorkOrders/ConvertQuotationToWorkOrder/ConvertQuotationToWorkOrderHandler.cs`, `src/Agendamento.Api/Application/WorkOrders/ConvertQuotationToWorkOrder/ConvertQuotationToWorkOrderCommand.cs`, `src/Agendamento.Api/Features/Quotations/QuotationEndpoints.cs` |

### Onda 2 — faixa-4

#### faixa-4 — branch `spec/gestao-orcamentos-servicos-faixa-4` — worktree `../onp-worktrees/agendamento-gestao-orcamentos-servicos-faixa-4`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-092 | Testes de Integração para Orçamentos | `gpt-5.6-terra` | medium | `tests/Agendamento.IntegrationTests/Features/QuotationTests.cs`, `tests/Agendamento.UnitTests/Quotations/QuotationDomainTests.cs` |

## Gestão de branches e commits

1. branch de trabalho `spec/gestao-orcamentos-servicos` criada do ponto atual (se ainda não existir)
2. cada faixa nasce dela como branch própria e roda no seu worktree — **1 tarefa = 1 commit** (`T-xxx feature: título`)
3. terminou a onda → merge `--no-ff` de cada faixa de volta, na ordem; conflito interrompe a faixa e pede resolução humana
4. faixa mesclada → worktree removido, branch apagada, tarefa marcada `[concluida]` no tasks.md
5. gate final na branch de trabalho: `onp-spec verify gestao-orcamentos-servicos` + `onp-spec audit --ci` — **exit 0 ou não está pronto**

## Como executar

### ▶ Execução — Codex headless (codex exec)

```bash
bash .spec/features/gestao-orcamentos-servicos/executar-tarefas.sh
```

Cada faixa roda `codex exec` com **janela de contexto limpa**, no seu worktree, com
`--model` e `model_reasoning_effort` já definidos por tarefa e sandbox `workspace-write`. Os prompts exatos estão
embutidos no script — quer rodar uma faixa na mão, é só copiá-los de lá.
Logs: `../onp-worktrees/agendamento-gestao-orcamentos-servicos-logs/`.

**Confirmação de custos — antes de executar**: os modelos e esforços por
tarefa estão nas tabelas acima; o agente CONFIRMA com o usuário se estão
dentro da licença/cota dele (modelo forte + esforço alto torra tokens).
Para gastar menos: `onp-spec plano gestao-orcamentos-servicos --modelo gpt-5.6-luna --esforco baixo`
(tudo) ou por tarefa `onp-spec tarefa gestao-orcamentos-servicos T-xxx --modelo <m> --esforco <nível>` — e regenere o plano.

### 📣 Acompanhamento — tabela + resumo no chat (a cada 1 min)

O script roda em **background**: o agente AVISA o usuário antes de iniciar e,
enquanto roda, posta no chat a cada ~1 minuto a **tabela de andamento** (qual
tarefa está rodando, qual não está, o que concluiu/falhou) junto com o
**resumo geral de andamento** (escrito por IA; sem IA, o motor resume). Ao
final, o usuário recebe o resumo completo da execução. A qualquer momento:

```bash
onp-spec resumo gestao-orcamentos-servicos --tabela   # a tabela de andamento
onp-spec resumo gestao-orcamentos-servicos            # o resumo em texto
```

