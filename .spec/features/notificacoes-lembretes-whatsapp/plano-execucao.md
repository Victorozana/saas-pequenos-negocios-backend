# Plano de execução — notificacoes-lembretes-whatsapp

> gerado por `onp-spec plano` em 2026-08-11 19:28 — NÃO edite à mão;
> mudou tasks.md ou a config? Regenere: `onp-spec plano notificacoes-lembretes-whatsapp`

## Resumo — o que vai acontecer

- **4 tarefa(s) pendente(s)**: 4 em 4 faixa(s) paralela(s) + 0 sequencial(is)
- **1 faixa = 1 worktree + 1 branch + 1 janela de contexto limpa** — faixas não compartilham nenhum arquivo entre si
- prefere outra seleção ou uma após a outra? Regenere com `onp-spec plano notificacoes-lembretes-whatsapp --paralelizar T-xxx,T-yyy` ou `--sequencial`
- tudo acontece na branch de trabalho `spec/notificacoes-lembretes-whatsapp`; levar para a main é decisão sua

## Faixas e ondas

### Onda 1 — faixa-1 ∥ faixa-2 ∥ faixa-3

#### faixa-1 — branch `spec/notificacoes-lembretes-whatsapp-faixa-1` — worktree `../onp-worktrees/agendamento-notificacoes-lembretes-whatsapp-faixa-1`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-097 | Serviço Abstrato e Entidades de Notificação | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Application/Notifications/INotificationService.cs`, `src/Agendamento.Api/Domain/Notifications/NotificationRecord.cs`, `src/Agendamento.Api/Infrastructure/Notifications/ConsoleNotificationService.cs` |

#### faixa-2 — branch `spec/notificacoes-lembretes-whatsapp-faixa-2` — worktree `../onp-worktrees/agendamento-notificacoes-lembretes-whatsapp-faixa-2`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-098 | Mapeamento EF Core de Notificações | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Infrastructure/Persistence/Configurations/NotificationRecordConfiguration.cs`, `src/Agendamento.Api/Infrastructure/Persistence/AgendamentoDbContext.cs` |

#### faixa-3 — branch `spec/notificacoes-lembretes-whatsapp-faixa-3` — worktree `../onp-worktrees/agendamento-notificacoes-lembretes-whatsapp-faixa-3`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-099 | Integração de Notificação no Agendamento | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Application/Appointments/CreateAppointment/CreateAppointmentHandler.cs` |

### Onda 2 — faixa-4

#### faixa-4 — branch `spec/notificacoes-lembretes-whatsapp-faixa-4` — worktree `../onp-worktrees/agendamento-notificacoes-lembretes-whatsapp-faixa-4`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-100 | Testes de Integração de Notificação | `gpt-5.6-terra` | medium | `tests/Agendamento.IntegrationTests/Features/NotificationTests.cs` |

## Gestão de branches e commits

1. branch de trabalho `spec/notificacoes-lembretes-whatsapp` criada do ponto atual (se ainda não existir)
2. cada faixa nasce dela como branch própria e roda no seu worktree — **1 tarefa = 1 commit** (`T-xxx feature: título`)
3. terminou a onda → merge `--no-ff` de cada faixa de volta, na ordem; conflito interrompe a faixa e pede resolução humana
4. faixa mesclada → worktree removido, branch apagada, tarefa marcada `[concluida]` no tasks.md
5. gate final na branch de trabalho: `onp-spec verify notificacoes-lembretes-whatsapp` + `onp-spec audit --ci` — **exit 0 ou não está pronto**

## Como executar

### ▶ Execução — Codex headless (codex exec)

```bash
bash .spec/features/notificacoes-lembretes-whatsapp/executar-tarefas.sh
```

Cada faixa roda `codex exec` com **janela de contexto limpa**, no seu worktree, com
`--model` e `model_reasoning_effort` já definidos por tarefa e sandbox `workspace-write`. Os prompts exatos estão
embutidos no script — quer rodar uma faixa na mão, é só copiá-los de lá.
Logs: `../onp-worktrees/agendamento-notificacoes-lembretes-whatsapp-logs/`.

**Confirmação de custos — antes de executar**: os modelos e esforços por
tarefa estão nas tabelas acima; o agente CONFIRMA com o usuário se estão
dentro da licença/cota dele (modelo forte + esforço alto torra tokens).
Para gastar menos: `onp-spec plano notificacoes-lembretes-whatsapp --modelo gpt-5.6-luna --esforco baixo`
(tudo) ou por tarefa `onp-spec tarefa notificacoes-lembretes-whatsapp T-xxx --modelo <m> --esforco <nível>` — e regenere o plano.

### 📣 Acompanhamento — tabela + resumo no chat (a cada 1 min)

O script roda em **background**: o agente AVISA o usuário antes de iniciar e,
enquanto roda, posta no chat a cada ~1 minuto a **tabela de andamento** (qual
tarefa está rodando, qual não está, o que concluiu/falhou) junto com o
**resumo geral de andamento** (escrito por IA; sem IA, o motor resume). Ao
final, o usuário recebe o resumo completo da execução. A qualquer momento:

```bash
onp-spec resumo notificacoes-lembretes-whatsapp --tabela   # a tabela de andamento
onp-spec resumo notificacoes-lembretes-whatsapp            # o resumo em texto
```

