# Plano de execução — fundacao-api-testes

> gerado por `onp-spec plano` em 2026-08-01 19:01 — NÃO edite à mão;
> mudou tasks.md ou a config? Regenere: `onp-spec plano fundacao-api-testes`

## Resumo — o que vai acontecer

- **4 tarefa(s) pendente(s)**: 4 em 4 faixa(s) paralela(s) + 0 sequencial(is)
- **1 faixa = 1 worktree + 1 branch + 1 janela de contexto limpa** — faixas não compartilham nenhum arquivo entre si
- prefere outra seleção ou uma após a outra? Regenere com `onp-spec plano fundacao-api-testes --paralelizar T-xxx,T-yyy` ou `--sequencial`
- tudo acontece na branch de trabalho `spec/fundacao-api-testes`; levar para a main é decisão sua

## Faixas e ondas

### Onda 1 — faixa-1 ∥ faixa-2 ∥ faixa-3

#### faixa-1 — branch `spec/fundacao-api-testes-faixa-1` — worktree `../onp-worktrees/agendamento-fundacao-api-testes-faixa-1`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-001 | Organizar projetos e dependências .NET | `gpt-5.6-terra` | medium | `Agendamento.sln`, `Directory.Packages.props`, `src/Agendamento.Api/Agendamento.Api.csproj`, `src/Agendamento.Api/Application/DependencyInjection.cs`, `tests/Agendamento.UnitTests/Agendamento.UnitTests.csproj`, `tests/Agendamento.IntegrationTests/Agendamento.IntegrationTests.csproj`, `tests/Agendamento.ArchitectureTests/Agendamento.ArchitectureTests.csproj` |

#### faixa-2 — branch `spec/fundacao-api-testes-faixa-2` — worktree `../onp-worktrees/agendamento-fundacao-api-testes-faixa-2`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-002 | Criar host e prova HTTP reutilizável | `gpt-5.6-terra` | medium | `tests/Agendamento.IntegrationTests/Infrastructure/AgendamentoApiFactory.cs`, `tests/Agendamento.IntegrationTests/Health/HealthEndpointTests.cs` |

#### faixa-3 — branch `spec/fundacao-api-testes-faixa-3` — worktree `../onp-worktrees/agendamento-fundacao-api-testes-faixa-3`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-003 | Criar infraestrutura PostgreSQL isolada | `gpt-5.6-sol` | high | `tests/Agendamento.IntegrationTests/Infrastructure/PostgreSqlFixture.cs`, `tests/Agendamento.IntegrationTests/Infrastructure/PostgreSqlCollection.cs`, `tests/Agendamento.IntegrationTests/Persistence/PostgreSqlFixtureTests.cs` |

### Onda 2 — faixa-4

#### faixa-4 — branch `spec/fundacao-api-testes-faixa-4` — worktree `../onp-worktrees/agendamento-fundacao-api-testes-faixa-4`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-004 | Externalizar e validar configuração | `gpt-5.6-terra` | medium | `src/Agendamento.Api/appsettings.json`, `src/Agendamento.Api/appsettings.Development.json`, `src/Agendamento.Api/Infrastructure/DependencyInjection.cs`, `tests/Agendamento.ArchitectureTests/Configuration/SecretConfigurationTests.cs`, `tests/Agendamento.IntegrationTests/Configuration/DatabaseConfigurationTests.cs`, `onpspec.config.json` |

## Gestão de branches e commits

1. branch de trabalho `spec/fundacao-api-testes` criada do ponto atual (se ainda não existir)
2. cada faixa nasce dela como branch própria e roda no seu worktree — **1 tarefa = 1 commit** (`T-xxx feature: título`)
3. terminou a onda → merge `--no-ff` de cada faixa de volta, na ordem; conflito interrompe a faixa e pede resolução humana
4. faixa mesclada → worktree removido, branch apagada, tarefa marcada `[concluida]` no tasks.md
5. gate final na branch de trabalho: `onp-spec verify fundacao-api-testes` + `onp-spec audit --ci` — **exit 0 ou não está pronto**

## Como executar

### ▶ Execução — Codex headless (codex exec)

```bash
bash .spec/features/fundacao-api-testes/executar-tarefas.sh
```

Cada faixa roda `codex exec` com **janela de contexto limpa**, no seu worktree, com
`--model` e `model_reasoning_effort` já definidos por tarefa e sandbox `workspace-write`. Os prompts exatos estão
embutidos no script — quer rodar uma faixa na mão, é só copiá-los de lá.
Logs: `../onp-worktrees/agendamento-fundacao-api-testes-logs/`.

**Confirmação de custos — antes de executar**: os modelos e esforços por
tarefa estão nas tabelas acima; o agente CONFIRMA com o usuário se estão
dentro da licença/cota dele (modelo forte + esforço alto torra tokens).
Para gastar menos: `onp-spec plano fundacao-api-testes --modelo gpt-5.6-luna --esforco baixo`
(tudo) ou por tarefa `onp-spec tarefa fundacao-api-testes T-xxx --modelo <m> --esforco <nível>` — e regenere o plano.

### 📣 Acompanhamento — tabela + resumo no chat (a cada 1 min)

O script roda em **background**: o agente AVISA o usuário antes de iniciar e,
enquanto roda, posta no chat a cada ~1 minuto a **tabela de andamento** (qual
tarefa está rodando, qual não está, o que concluiu/falhou) junto com o
**resumo geral de andamento** (escrito por IA; sem IA, o motor resume). Ao
final, o usuário recebe o resumo completo da execução. A qualquer momento:

```bash
onp-spec resumo fundacao-api-testes --tabela   # a tabela de andamento
onp-spec resumo fundacao-api-testes            # o resumo em texto
```

