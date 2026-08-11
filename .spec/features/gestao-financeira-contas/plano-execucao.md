# Plano de execução — gestao-financeira-contas

> gerado por `onp-spec plano` em 2026-08-11 19:05 — NÃO edite à mão;
> mudou tasks.md ou a config? Regenere: `onp-spec plano gestao-financeira-contas`

## Resumo — o que vai acontecer

- **4 tarefa(s) pendente(s)**: 4 em 4 faixa(s) paralela(s) + 0 sequencial(is)
- **1 faixa = 1 worktree + 1 branch + 1 janela de contexto limpa** — faixas não compartilham nenhum arquivo entre si
- prefere outra seleção ou uma após a outra? Regenere com `onp-spec plano gestao-financeira-contas --paralelizar T-xxx,T-yyy` ou `--sequencial`
- tudo acontece na branch de trabalho `spec/gestao-financeira-contas`; levar para a main é decisão sua

## Faixas e ondas

### Onda 1 — faixa-1 ∥ faixa-2 ∥ faixa-3

#### faixa-1 — branch `spec/gestao-financeira-contas-faixa-1` — worktree `../onp-worktrees/agendamento-gestao-financeira-contas-faixa-1`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-093 | Entidades de Domínio para Financeiro | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Domain/Financial/Transaction.cs`, `src/Agendamento.Api/Domain/Financial/TransactionType.cs`, `src/Agendamento.Api/Domain/Financial/TransactionStatus.cs` |

#### faixa-2 — branch `spec/gestao-financeira-contas-faixa-2` — worktree `../onp-worktrees/agendamento-gestao-financeira-contas-faixa-2`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-094 | Mapeamento EF Core para Financeiro | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Infrastructure/Persistence/Configurations/TransactionConfiguration.cs`, `src/Agendamento.Api/Infrastructure/Persistence/AgendamentoDbContext.cs` |

#### faixa-3 — branch `spec/gestao-financeira-contas-faixa-3` — worktree `../onp-worktrees/agendamento-gestao-financeira-contas-faixa-3`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-095 | Caso de Uso e Endpoints de Financeiro | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Features/Financial/FinancialEndpoints.cs`, `src/Agendamento.Api/Program.cs`, `src/Agendamento.Api/Application/Financial/PayTransaction/PayTransactionHandler.cs`, `src/Agendamento.Api/Application/Financial/CreateTransaction/CreateTransactionHandler.cs` |

### Onda 2 — faixa-4

#### faixa-4 — branch `spec/gestao-financeira-contas-faixa-4` — worktree `../onp-worktrees/agendamento-gestao-financeira-contas-faixa-4`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-096 | Testes de Integração para Financeiro | `gpt-5.6-terra` | medium | `tests/Agendamento.IntegrationTests/Features/FinancialTests.cs`, `tests/Agendamento.UnitTests/Financial/FinancialDomainTests.cs` |

## Gestão de branches e commits

1. branch de trabalho `spec/gestao-financeira-contas` criada do ponto atual (se ainda não existir)
2. cada faixa nasce dela como branch própria e roda no seu worktree — **1 tarefa = 1 commit** (`T-xxx feature: título`)
3. terminou a onda → merge `--no-ff` de cada faixa de volta, na ordem; conflito interrompe a faixa e pede resolução humana
4. faixa mesclada → worktree removido, branch apagada, tarefa marcada `[concluida]` no tasks.md
5. gate final na branch de trabalho: `onp-spec verify gestao-financeira-contas` + `onp-spec audit --ci` — **exit 0 ou não está pronto**

## Como executar

### ▶ Execução — Codex headless (codex exec)

```bash
bash .spec/features/gestao-financeira-contas/executar-tarefas.sh
```

Cada faixa roda `codex exec` com **janela de contexto limpa**, no seu worktree, com
`--model` e `model_reasoning_effort` já definidos por tarefa e sandbox `workspace-write`. Os prompts exatos estão
embutidos no script — quer rodar uma faixa na mão, é só copiá-los de lá.
Logs: `../onp-worktrees/agendamento-gestao-financeira-contas-logs/`.

**Confirmação de custos — antes de executar**: os modelos e esforços por
tarefa estão nas tabelas acima; o agente CONFIRMA com o usuário se estão
dentro da licença/cota dele (modelo forte + esforço alto torra tokens).
Para gastar menos: `onp-spec plano gestao-financeira-contas --modelo gpt-5.6-luna --esforco baixo`
(tudo) ou por tarefa `onp-spec tarefa gestao-financeira-contas T-xxx --modelo <m> --esforco <nível>` — e regenere o plano.

### 📣 Acompanhamento — tabela + resumo no chat (a cada 1 min)

O script roda em **background**: o agente AVISA o usuário antes de iniciar e,
enquanto roda, posta no chat a cada ~1 minuto a **tabela de andamento** (qual
tarefa está rodando, qual não está, o que concluiu/falhou) junto com o
**resumo geral de andamento** (escrito por IA; sem IA, o motor resume). Ao
final, o usuário recebe o resumo completo da execução. A qualquer momento:

```bash
onp-spec resumo gestao-financeira-contas --tabela   # a tabela de andamento
onp-spec resumo gestao-financeira-contas            # o resumo em texto
```

