# Plano de execução — identidade-acesso-inicial

> gerado por `onp-spec plano` em 2026-08-01 20:04 — NÃO edite à mão;
> mudou tasks.md ou a config? Regenere: `onp-spec plano identidade-acesso-inicial`

## Resumo — o que vai acontecer

- **5 tarefa(s) pendente(s)**: 5 em 5 faixa(s) paralela(s) + 0 sequencial(is)
- **1 faixa = 1 worktree + 1 branch + 1 janela de contexto limpa** — faixas não compartilham nenhum arquivo entre si
- prefere outra seleção ou uma após a outra? Regenere com `onp-spec plano identidade-acesso-inicial --paralelizar T-xxx,T-yyy` ou `--sequencial`
- tudo acontece na branch de trabalho `spec/identidade-acesso-inicial`; levar para a main é decisão sua

## Faixas e ondas

### Onda 1 — faixa-1 ∥ faixa-2 ∥ faixa-3

#### faixa-1 — branch `spec/identidade-acesso-inicial-faixa-1` — worktree `../onp-worktrees/agendamento-identidade-acesso-inicial-faixa-1`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-005 | Modelar usuário e credenciais | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Domain/Identity/User.cs`, `src/Agendamento.Api/Domain/Identity/UserStatus.cs`, `src/Agendamento.Api/Infrastructure/Persistence/Configurations/UserConfiguration.cs`, `tests/Agendamento.UnitTests/Identity/UserTests.cs`, `tests/Agendamento.IntegrationTests/Identity/UserPersistenceTests.cs` |

#### faixa-2 — branch `spec/identidade-acesso-inicial-faixa-2` — worktree `../onp-worktrees/agendamento-identidade-acesso-inicial-faixa-2`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-006 | Persistir tokens de confirmação de e-mail | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Domain/Identity/EmailVerificationToken.cs`, `src/Agendamento.Api/Application/Identity/VerifyEmail/VerifyEmailCommand.cs`, `src/Agendamento.Api/Application/Identity/VerifyEmail/VerifyEmailHandler.cs`, `src/Agendamento.Api/Infrastructure/Persistence/Configurations/EmailVerificationTokenConfiguration.cs`, `tests/Agendamento.UnitTests/Identity/EmailVerificationTokenTests.cs` |

#### faixa-3 — branch `spec/identidade-acesso-inicial-faixa-3` — worktree `../onp-worktrees/agendamento-identidade-acesso-inicial-faixa-3`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-007 | Criar porta de envio e caixa de saída de e-mail | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Application/Identity/IEmailVerificationSender.cs`, `src/Agendamento.Api/Infrastructure/Email/OutboxEmailVerificationSender.cs`, `src/Agendamento.Api/Infrastructure/Persistence/Entities/EmailOutboxMessage.cs`, `tests/Agendamento.IntegrationTests/Identity/EmailVerificationOutboxTests.cs` |

### Onda 2 — faixa-4 ∥ faixa-5

#### faixa-4 — branch `spec/identidade-acesso-inicial-faixa-4` — worktree `../onp-worktrees/agendamento-identidade-acesso-inicial-faixa-4`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-008 | Expor confirmação de e-mail | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Features/Identity/EmailVerificationEndpoints.cs`, `src/Agendamento.Api/Features/Identity/VerifyEmailRequest.cs`, `tests/Agendamento.IntegrationTests/Identity/EmailVerificationEndpointTests.cs` |

#### faixa-5 — branch `spec/identidade-acesso-inicial-faixa-5` — worktree `../onp-worktrees/agendamento-identidade-acesso-inicial-faixa-5`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-009 | Autenticar e emitir sessão | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Application/Identity/CreateSession/CreateSessionCommand.cs`, `src/Agendamento.Api/Application/Identity/CreateSession/CreateSessionHandler.cs`, `src/Agendamento.Api/Application/Identity/ISessionIssuer.cs`, `src/Agendamento.Api/Infrastructure/Identity/JwtSessionIssuer.cs`, `src/Agendamento.Api/Features/Identity/SessionEndpoints.cs`, `tests/Agendamento.IntegrationTests/Identity/SessionEndpointTests.cs` |

## Gestão de branches e commits

1. branch de trabalho `spec/identidade-acesso-inicial` criada do ponto atual (se ainda não existir)
2. cada faixa nasce dela como branch própria e roda no seu worktree — **1 tarefa = 1 commit** (`T-xxx feature: título`)
3. terminou a onda → merge `--no-ff` de cada faixa de volta, na ordem; conflito interrompe a faixa e pede resolução humana
4. faixa mesclada → worktree removido, branch apagada, tarefa marcada `[concluida]` no tasks.md
5. gate final na branch de trabalho: `onp-spec verify identidade-acesso-inicial` + `onp-spec audit --ci` — **exit 0 ou não está pronto**

## Como executar

### ▶ Execução — Codex headless (codex exec)

```bash
bash .spec/features/identidade-acesso-inicial/executar-tarefas.sh
```

Cada faixa roda `codex exec` com **janela de contexto limpa**, no seu worktree, com
`--model` e `model_reasoning_effort` já definidos por tarefa e sandbox `workspace-write`. Os prompts exatos estão
embutidos no script — quer rodar uma faixa na mão, é só copiá-los de lá.
Logs: `../onp-worktrees/agendamento-identidade-acesso-inicial-logs/`.

**Confirmação de custos — antes de executar**: os modelos e esforços por
tarefa estão nas tabelas acima; o agente CONFIRMA com o usuário se estão
dentro da licença/cota dele (modelo forte + esforço alto torra tokens).
Para gastar menos: `onp-spec plano identidade-acesso-inicial --modelo gpt-5.6-luna --esforco baixo`
(tudo) ou por tarefa `onp-spec tarefa identidade-acesso-inicial T-xxx --modelo <m> --esforco <nível>` — e regenere o plano.

### 📣 Acompanhamento — tabela + resumo no chat (a cada 1 min)

O script roda em **background**: o agente AVISA o usuário antes de iniciar e,
enquanto roda, posta no chat a cada ~1 minuto a **tabela de andamento** (qual
tarefa está rodando, qual não está, o que concluiu/falhou) junto com o
**resumo geral de andamento** (escrito por IA; sem IA, o motor resume). Ao
final, o usuário recebe o resumo completo da execução. A qualquer momento:

```bash
onp-spec resumo identidade-acesso-inicial --tabela   # a tabela de andamento
onp-spec resumo identidade-acesso-inicial            # o resumo em texto
```

