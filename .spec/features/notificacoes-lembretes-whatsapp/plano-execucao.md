# Plano de execução — notificacoes-lembretes-whatsapp

> gerado por `onp-spec plano` em 2026-10-12 19:39 — NÃO edite à mão;
> mudou tasks.md ou a config? Regenere: `onp-spec plano notificacoes-lembretes-whatsapp --sequencial`

## Resumo — o que vai acontecer

- **modo SEQUENCIAL (escolha do usuário)**: 6 tarefa(s) pendente(s), UMA APÓS A OUTRA, na árvore principal (6 já concluída(s): T-097, T-098, T-099, T-100, T-104, T-105)
- sem worktrees e sem paralelismo — cada tarefa roda numa janela de contexto limpa, na ordem do tasks.md
- tudo acontece na branch de trabalho `spec/notificacoes-lembretes-whatsapp`; levar para a main é decisão sua

## Ordem de execução (uma tarefa após a outra)

| tarefa | título | modelo | esforço |
|---|---|---|---|
| T-052 | Modelar Entidades de Notificação e Outbox (NotificationMessage, OutboxMessage) | `gpt-5.6-terra` | medium |
| T-053 | Mapear Persistência EF Core e Outbox Pattern | `gpt-5.6-terra` | medium |
| T-054 | Implementar Provedor de Envio de Mensagens e Dispatcher Background | `gpt-5.6-terra` | medium |
| T-055 | Casos de Uso: Agendamento de Lembretes e Notificação de Orçamento | `gpt-5.6-terra` | medium |
| T-056 | Expor Endpoints Minimal API de Notificações e Histórico | `gpt-5.6-terra` | medium |
| T-057 | Testes de Integração do Outbox Processor e Formatação de Mensagens | `gpt-5.6-terra` | medium |

## Gestão de branches e commits

1. branch de trabalho `spec/notificacoes-lembretes-whatsapp` criada do ponto atual (se ainda não existir)
2. as tarefas rodam nela mesma, na ordem — **1 tarefa = 1 commit** (`T-xxx feature: título`), marcada `[concluida]` só com trabalho feito
3. gate final na branch de trabalho: `onp-spec verify notificacoes-lembretes-whatsapp` + `onp-spec audit --ci` — **exit 0 ou não está pronto**

## Como executar

### ▶ Execução — Codex headless (codex exec)

```bash
bash .spec/features/notificacoes-lembretes-whatsapp/executar-tarefas.sh
```

Cada tarefa roda `codex exec` com **janela de contexto limpa**, na árvore principal,
uma após a outra, com `--model` e `model_reasoning_effort` já definidos por tarefa e sandbox `workspace-write`.
Os prompts exatos estão embutidos no script.
Logs: `../onp-worktrees/saas-pequenos-negocios-backend-notificacoes-lembretes-whatsapp-logs/`.

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

