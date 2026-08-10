# Plano de execução — gestao-clientes

> gerado por `onp-spec plano` em 2026-08-10 03:39 — NÃO edite à mão;
> mudou tasks.md ou a config? Regenere: `onp-spec plano gestao-clientes`

## Resumo — o que vai acontecer

- **5 tarefa(s) pendente(s)**: 5 em 5 faixa(s) paralela(s) + 0 sequencial(is)
- **1 faixa = 1 worktree + 1 branch + 1 janela de contexto limpa** — faixas não compartilham nenhum arquivo entre si
- prefere outra seleção ou uma após a outra? Regenere com `onp-spec plano gestao-clientes --paralelizar T-xxx,T-yyy` ou `--sequencial`
- tudo acontece na branch de trabalho `spec/gestao-clientes`; levar para a main é decisão sua

## Faixas e ondas

### Onda 1 — faixa-1 ∥ faixa-2 ∥ faixa-3

#### faixa-1 — branch `spec/gestao-clientes-faixa-1` — worktree `../onp-worktrees/agendamento-gestao-clientes-faixa-1`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-026 | Modelar entidade Cliente e Value Objects | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Domain/Customers/Customer.cs`, `src/Agendamento.Api/Domain/Customers/CustomerDocument.cs`, `src/Agendamento.Api/Domain/Customers/CustomerAddress.cs` |

#### faixa-2 — branch `spec/gestao-clientes-faixa-2` — worktree `../onp-worktrees/agendamento-gestao-clientes-faixa-2`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-027 | Mapear persistência e Migration de Clientes | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Infrastructure/Persistence/AgendamentoDbContext.cs`, `src/Agendamento.Api/Infrastructure/Persistence/Configurations/CustomerConfiguration.cs` |

#### faixa-3 — branch `spec/gestao-clientes-faixa-3` — worktree `../onp-worktrees/agendamento-gestao-clientes-faixa-3`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-028 | Orquestrar Casos de Uso de Clientes (Handlers) | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Application/Customers/CreateCustomer/`, `src/Agendamento.Api/Application/Customers/GetCustomers/`, `src/Agendamento.Api/Application/Customers/UpdateCustomer/` |

### Onda 2 — faixa-4 ∥ faixa-5

#### faixa-4 — branch `spec/gestao-clientes-faixa-4` — worktree `../onp-worktrees/agendamento-gestao-clientes-faixa-4`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-029 | Expor Endpoints Minimal API de Clientes | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Features/Customers/CustomerEndpoints.cs`, `src/Agendamento.Api/Program.cs` |

#### faixa-5 — branch `spec/gestao-clientes-faixa-5` — worktree `../onp-worktrees/agendamento-gestao-clientes-faixa-5`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-030 | Testes de Integração e Isolamento de Clientes | `gpt-5.6-terra` | medium | `tests/Agendamento.IntegrationTests/Customers/CustomerApiTests.cs` |

## Gestão de branches e commits

1. branch de trabalho `spec/gestao-clientes` criada do ponto atual (se ainda não existir)
2. cada faixa nasce dela como branch própria e roda no seu worktree — **1 tarefa = 1 commit** (`T-xxx feature: título`)
3. terminou a onda → merge `--no-ff` de cada faixa de volta, na ordem; conflito interrompe a faixa e pede resolução humana
4. faixa mesclada → worktree removido, branch apagada, tarefa marcada `[concluida]` no tasks.md
5. gate final na branch de trabalho: `onp-spec verify gestao-clientes` + `onp-spec audit --ci` — **exit 0 ou não está pronto**

## Como executar

### ▶ Execução — Codex headless (codex exec)

```bash
bash .spec/features/gestao-clientes/executar-tarefas.sh
```

Cada faixa roda `codex exec` com **janela de contexto limpa**, no seu worktree, com
`--model` e `model_reasoning_effort` já definidos por tarefa e sandbox `workspace-write`. Os prompts exatos estão
embutidos no script — quer rodar uma faixa na mão, é só copiá-los de lá.
Logs: `../onp-worktrees/agendamento-gestao-clientes-logs/`.

**Confirmação de custos — antes de executar**: os modelos e esforços por
tarefa estão nas tabelas acima; o agente CONFIRMA com o usuário se estão
dentro da licença/cota dele (modelo forte + esforço alto torra tokens).
Para gastar menos: `onp-spec plano gestao-clientes --modelo gpt-5.6-luna --esforco baixo`
(tudo) ou por tarefa `onp-spec tarefa gestao-clientes T-xxx --modelo <m> --esforco <nível>` — e regenere o plano.

### 📣 Acompanhamento — tabela + resumo no chat (a cada 1 min)

O script roda em **background**: o agente AVISA o usuário antes de iniciar e,
enquanto roda, posta no chat a cada ~1 minuto a **tabela de andamento** (qual
tarefa está rodando, qual não está, o que concluiu/falhou) junto com o
**resumo geral de andamento** (escrito por IA; sem IA, o motor resume). Ao
final, o usuário recebe o resumo completo da execução. A qualquer momento:

```bash
onp-spec resumo gestao-clientes --tabela   # a tabela de andamento
onp-spec resumo gestao-clientes            # o resumo em texto
```

