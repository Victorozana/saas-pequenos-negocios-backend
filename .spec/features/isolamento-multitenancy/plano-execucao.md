# Plano de execução — isolamento-multitenancy

> gerado por `onp-spec plano` em 2026-08-01 18:48 — NÃO edite à mão;
> mudou tasks.md ou a config? Regenere: `onp-spec plano isolamento-multitenancy`

## Resumo — o que vai acontecer

- **5 tarefa(s) pendente(s)**: 5 em 5 faixa(s) paralela(s) + 0 sequencial(is)
- **1 faixa = 1 worktree + 1 branch + 1 janela de contexto limpa** — faixas não compartilham nenhum arquivo entre si
- prefere outra seleção ou uma após a outra? Regenere com `onp-spec plano isolamento-multitenancy --paralelizar T-xxx,T-yyy` ou `--sequencial`
- tudo acontece na branch de trabalho `spec/isolamento-multitenancy`; levar para a main é decisão sua

## Faixas e ondas

### Onda 1 — faixa-1 ∥ faixa-2 ∥ faixa-3

#### faixa-1 — branch `spec/isolamento-multitenancy-faixa-1` — worktree `../onp-worktrees/agendamento-isolamento-multitenancy-faixa-1`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-017 | Resolver contexto de tenant autenticado | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Application/Tenancy/ITenantContext.cs`, `src/Agendamento.Api/Infrastructure/Tenancy/HttpTenantContext.cs`, `src/Agendamento.Api/Infrastructure/Tenancy/TenantContextMiddleware.cs`, `tests/Agendamento.IntegrationTests/Tenancy/TenantContextTests.cs` |

#### faixa-2 — branch `spec/isolamento-multitenancy-faixa-2` — worktree `../onp-worktrees/agendamento-isolamento-multitenancy-faixa-2`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-018 | Aplicar filtros e guarda de gravação do EF Core | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Domain/Common/ITenantOwned.cs`, `src/Agendamento.Api/Infrastructure/Persistence/AgendamentoDbContext.cs`, `src/Agendamento.Api/Infrastructure/Persistence/TenantSaveChangesInterceptor.cs`, `tests/Agendamento.IntegrationTests/Tenancy/EfTenantIsolationTests.cs` |

#### faixa-3 — branch `spec/isolamento-multitenancy-faixa-3` — worktree `../onp-worktrees/agendamento-isolamento-multitenancy-faixa-3`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-019 | Criar papel, contexto transacional e policies RLS | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Infrastructure/Persistence/Migrations`, `src/Agendamento.Api/Infrastructure/Persistence/TenantTransactionInterceptor.cs`, `src/Agendamento.Api/Infrastructure/Persistence/Sql/TenantRls.sql`, `tests/Agendamento.IntegrationTests/Tenancy/PostgreSqlRlsTests.cs` |

### Onda 2 — faixa-4 ∥ faixa-5

#### faixa-4 — branch `spec/isolamento-multitenancy-faixa-4` — worktree `../onp-worktrees/agendamento-isolamento-multitenancy-faixa-4`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-020 | Provar isolamento completo entre dois tenants | `gpt-5.6-terra` | medium | `tests/Agendamento.IntegrationTests/Tenancy/CrossTenantIsolationTests.cs` |

#### faixa-5 — branch `spec/isolamento-multitenancy-faixa-5` — worktree `../onp-worktrees/agendamento-isolamento-multitenancy-faixa-5`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-021 | Impedir migration tenant-owned desprotegida | `gpt-5.6-terra` | medium | `tests/Agendamento.ArchitectureTests/Tenancy/TenantOwnedMigrationTests.cs` |

## Gestão de branches e commits

1. branch de trabalho `spec/isolamento-multitenancy` criada do ponto atual (se ainda não existir)
2. cada faixa nasce dela como branch própria e roda no seu worktree — **1 tarefa = 1 commit** (`T-xxx feature: título`)
3. terminou a onda → merge `--no-ff` de cada faixa de volta, na ordem; conflito interrompe a faixa e pede resolução humana
4. faixa mesclada → worktree removido, branch apagada, tarefa marcada `[concluida]` no tasks.md
5. gate final na branch de trabalho: `onp-spec verify isolamento-multitenancy` + `onp-spec audit --ci` — **exit 0 ou não está pronto**

## Como executar

### ▶ Execução — Codex headless (codex exec)

```bash
bash .spec/features/isolamento-multitenancy/executar-tarefas.sh
```

Cada faixa roda `codex exec` com **janela de contexto limpa**, no seu worktree, com
`--model` e `model_reasoning_effort` já definidos por tarefa e sandbox `workspace-write`. Os prompts exatos estão
embutidos no script — quer rodar uma faixa na mão, é só copiá-los de lá.
Logs: `../onp-worktrees/agendamento-isolamento-multitenancy-logs/`.

**Confirmação de custos — antes de executar**: os modelos e esforços por
tarefa estão nas tabelas acima; o agente CONFIRMA com o usuário se estão
dentro da licença/cota dele (modelo forte + esforço alto torra tokens).
Para gastar menos: `onp-spec plano isolamento-multitenancy --modelo gpt-5.6-luna --esforco baixo`
(tudo) ou por tarefa `onp-spec tarefa isolamento-multitenancy T-xxx --modelo <m> --esforco <nível>` — e regenere o plano.

### 📣 Acompanhamento — tabela + resumo no chat (a cada 1 min)

O script roda em **background**: o agente AVISA o usuário antes de iniciar e,
enquanto roda, posta no chat a cada ~1 minuto a **tabela de andamento** (qual
tarefa está rodando, qual não está, o que concluiu/falhou) junto com o
**resumo geral de andamento** (escrito por IA; sem IA, o motor resume). Ao
final, o usuário recebe o resumo completo da execução. A qualquer momento:

```bash
onp-spec resumo isolamento-multitenancy --tabela   # a tabela de andamento
onp-spec resumo isolamento-multitenancy            # o resumo em texto
```

