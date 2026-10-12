#!/usr/bin/env bash
# executar-tarefas.sh — gerado por `onp-spec plano notificacoes-lembretes-whatsapp` em 2026-10-12 19:39
# NÃO edite à mão: mudou tasks.md ou a config, regenere o plano.
#
# uso:
#   bash executar-tarefas.sh                  tudo (ondas → sequenciais → gate)
#   bash executar-tarefas.sh --faixa <id>     reexecuta UMA faixa (+ merge + gate)
#   bash executar-tarefas.sh --seq <T-xxx>    reexecuta UMA tarefa sequencial
#   bash executar-tarefas.sh --gate           só o gate (verify + audit)
#   bash executar-tarefas.sh --listar         mostra faixas, tarefas e estados
#   (acrescente --sem-gate para não rodar o gate ao final)
#
# resumo do que está rolando, a qualquer momento: onp-spec resumo notificacoes-lembretes-whatsapp
set -u
set -o pipefail

RUN_ID='saas-pequenos-negocios-backend-notificacoes-lembretes-whatsapp-mv5norhk'
FEATURE='notificacoes-lembretes-whatsapp'
BASE_BRANCH='spec/notificacoes-lembretes-whatsapp'
ENGINE='.agents/skills/onp-spec-driven/scripts/onp-spec.mjs'
CODEX_FLAGS=(--sandbox 'workspace-write')
STREAM_FLAGS=(--json)
FALHAS=""
COM_GATE=1
RESUMO_MODEL='gpt-5.6-luna'
RESUMO_PID=""

verde()    { printf '\033[32m%s\033[0m\n' "$*"; }
vermelho() { printf '\033[31m%s\033[0m\n' "$*"; }
amarelo()  { printf '\033[33m%s\033[0m\n' "$*"; }
info()     { printf '· %s\n' "$*"; }
falhar()   { vermelho "✘ $*"; exit 1; }

# eventos vão para o ledger GLOBAL (~/.onp-spec/painel/ledger.jsonl):
# um arquivo para todos os projetos, é o que o onp-spec resumo lê
evento() { node "$ENGINE" evento --run "$RUN_ID" "$@" >/dev/null 2>&1 || true; }

# ── ambiente (todos os modos passam por aqui) ────────────────────────
preparar_ambiente() {
  command -v git >/dev/null 2>&1 || falhar "git não encontrado"
  command -v node >/dev/null 2>&1 || falhar "node não encontrado"
  command -v codex >/dev/null 2>&1 || falhar "Codex CLI (codex) não encontrado — instale-o ou siga o modo manual em plano-execucao.md"
  TOPLEVEL=$(git rev-parse --show-toplevel 2>/dev/null) || falhar "fora de um repositório git"
  cd "$TOPLEVEL" || exit 1
  # artefatos recém-gerados pelo `onp-spec plano` são sujeira esperada:
  # se forem a ÚNICA sujeira, o script mesmo commita; qualquer outra, aborta
  if [ -n "$(git status --porcelain)" ]; then
    if [ -z "$(git status --porcelain | grep -v -e 'plano-execucao\.' -e 'plano\.json' -e 'executar-tarefas\.sh')" ]; then
      git add -A
      git commit -q -m "plano de execução: $FEATURE (artefatos gerados)"
      info "artefatos do plano commitados"
    else
      falhar "árvore suja além dos artefatos do plano — commite ou faça git stash antes (os worktrees partem do último commit)"
    fi
  fi
  git ls-files --error-unmatch -- '.spec/features/notificacoes-lembretes-whatsapp/spec.md' >/dev/null 2>&1 || falhar "spec.md não está commitada — os worktrees das faixas precisam dela no git"
  ATUAL=$(git rev-parse --abbrev-ref HEAD)
  [ "$ATUAL" != "HEAD" ] || falhar "HEAD destacado — troque para uma branch"
  if [ "$ATUAL" != "$BASE_BRANCH" ]; then
    if git show-ref --verify --quiet "refs/heads/$BASE_BRANCH"; then
      git checkout -q "$BASE_BRANCH" || falhar "não consegui trocar para $BASE_BRANCH"
    else
      git checkout -q -b "$BASE_BRANCH" || falhar "não consegui criar $BASE_BRANCH"
    fi
    info "branch de trabalho: $BASE_BRANCH (a partir de $ATUAL)"
  fi
  git worktree prune
  LOG_DIR="$(dirname "$TOPLEVEL")/onp-worktrees/saas-pequenos-negocios-backend-notificacoes-lembretes-whatsapp-logs"
  WT_BASE="$(dirname "$TOPLEVEL")/onp-worktrees/saas-pequenos-negocios-backend-notificacoes-lembretes-whatsapp"
  STREAMS_DIR="${ONP_SPEC_HOME:-$HOME/.onp-spec}/painel/streams/$RUN_ID"
  mkdir -p "$LOG_DIR" "$STREAMS_DIR"
}

# worktree limpo mesmo depois de uma tentativa que falhou
preparar_worktree() { # $1=faixa $2=branch $3=worktree
  git worktree prune
  if [ -e "$3" ]; then git worktree remove --force "$3" >/dev/null 2>&1; rm -rf "$3"; fi
  if git show-ref --verify --quiet "refs/heads/$2"; then git branch -D "$2" >/dev/null 2>&1; fi
  git worktree add "$3" -b "$2" >/dev/null 2>&1 || { vermelho "✘ não consegui criar o worktree de $1 em $3"; return 1; }
}

tentativa() { # $1=faixa — conta reexecuções (vai para o ledger)
  local arq="$LOG_DIR/.tentativa-$1"
  local n=1
  [ -f "$arq" ] && n=$(( $(cat "$arq") + 1 ))
  printf "%s" "$n" > "$arq"
  printf "%s" "$n"
}

# uma tarefa = uma sessão codex exec headless com contexto limpo.
# o JSONL da sessão vira o stream da tarefa no ledger
rodar_tarefa() { # $1=escopo(faixa|seq) $2=T-xxx $3=prompt $4=modelo $5=esforço
  local chave="$1--$2"
  local stream="$STREAMS_DIR/$chave.jsonl"
  evento --tipo tarefa --tarefa "$2" --faixa "$1" --estado executando --stream "$chave"
  info "$2 — codex exec ($4 · $5) · stream: $chave"
  # --add-dir: o .git compartilhado dos worktrees mora no repo principal —
  # sem ele o sandbox workspace-write bloquearia o commit da tarefa
  if codex exec "$3" --model "$4" -c model_reasoning_effort="$5" "${STREAM_FLAGS[@]}" "${CODEX_FLAGS[@]}" --add-dir "$TOPLEVEL" > "$stream" 2>>"$LOG_DIR/$1.log"; then
    evento --tipo tarefa --tarefa "$2" --faixa "$1" --estado concluida --stream "$chave"
    node "$ENGINE" stream-resumo "$RUN_ID" "$chave" 2>/dev/null || true
    return 0
  fi
  evento --tipo tarefa --tarefa "$2" --faixa "$1" --estado falhou --stream "$chave"
  node "$ENGINE" stream-resumo "$RUN_ID" "$chave" 2>/dev/null || true
  return 1
}

mesclar_faixa() { # $1=faixa $2=branch $3=worktree $4=exit-da-faixa
  if [ "$4" -ne 0 ]; then
    evento --tipo faixa --faixa "$1" --estado falhou
    vermelho "✘ $1 falhou (log: $LOG_DIR/$1.log) — worktree mantido para inspeção: $3"
    amarelo "  reexecute só ela: bash .spec/features/notificacoes-lembretes-whatsapp/executar-tarefas.sh --faixa $1"
    FALHAS="$FALHAS $1"; return 1
  fi
  evento --tipo faixa --faixa "$1" --estado mesclando
  if git merge --no-ff "$2" -m "merge $1 ($FEATURE)"; then
    git worktree remove --force "$3" >/dev/null 2>&1
    git branch -d "$2" >/dev/null 2>&1
    evento --tipo faixa --faixa "$1" --estado mesclada
    verde "✔ $1 mesclada em $BASE_BRANCH"
  else
    git merge --abort >/dev/null 2>&1
    evento --tipo faixa --faixa "$1" --estado conflito
    vermelho "✘ conflito ao mesclar $1 — resolva na mão: git merge $2 (worktree mantido: $3)"
    FALHAS="$FALHAS $1"; return 1
  fi
}

marcar_concluidas() { # $@=T-xxx
  for t in "$@"; do node "$ENGINE" tarefa "$FEATURE" "$t" concluida >/dev/null || true; done
}

# ── resumo geral de andamento: 1/min enquanto a execução roda ─────────
# escrito por IA (codex exec somente leitura) com fallback do motor; vai
# para o terminal e para o ledger — o agente repassa o texto no chat.
gerar_resumo() {
  local ctx ia
  ctx=$(node "$ENGINE" resumo "$FEATURE" --contexto 2>/dev/null) || ctx=""
  [ -n "$ctx" ] || return 0
  ia=$(codex exec "Você narra, para o dono do produto, uma execução de tarefas de código em andamento. Estado mecânico:

$ctx

Escreva o RESUMO GERAL DE ANDAMENTO: um parágrafo único de 2 a 4 frases, em português simples, dizendo o que está acontecendo agora, o que já terminou, o que falhou e se o usuário precisa agir. Sem markdown, sem listas." --model "$RESUMO_MODEL" --sandbox read-only --ephemeral 2>/dev/null)
  if [ -n "$ia" ]; then
    node "$ENGINE" resumo "$FEATURE" --gravar --origem ia --texto "$ia" >/dev/null 2>&1 || true
    printf '\n📣 resumo (IA): %s\n' "$ia"
  else
    node "$ENGINE" resumo "$FEATURE" --gravar >/dev/null 2>&1 || true
    printf '\n📣 resumo: %s\n' "$(node "$ENGINE" resumo "$FEATURE" 2>/dev/null)"
  fi
}

# mata o loop E o sleep filho — senão o sleep herda o stdout e quem chamou
# o script via pipe fica esperando EOF por até 60s depois do exit
parar_resumos() {
  [ -n "$RESUMO_PID" ] || return 0
  command -v pkill >/dev/null 2>&1 && pkill -P "$RESUMO_PID" 2>/dev/null
  kill "$RESUMO_PID" 2>/dev/null
  RESUMO_PID=""
}

iniciar_resumos() {
  ( while :; do sleep 60; gerar_resumo; done ) &
  RESUMO_PID=$!
  # ao sair: para o loop e grava um último resumo (o estado final, do motor)
  trap 'parar_resumos; node "$ENGINE" resumo "$FEATURE" --gravar >/dev/null 2>&1 || true' EXIT
}

# ── sequencial T-052 (ordem do tasks.md) ──
executar_seq_T_052() {
  info 'sequencial T-052 — Modelar Entidades de Notificação e Outbox (NotificationMessage, OutboxMessage)'
  if rodar_tarefa seq 'T-052' 'Você executa UMA tarefa da feature "notificacoes-lembretes-whatsapp" (fluxo onp-spec, spec-anchored).
Leia primeiro: .spec/features/notificacoes-lembretes-whatsapp/spec.md, .spec/features/notificacoes-lembretes-whatsapp/tasks.md e .spec/constituicao.md.

Sua tarefa (somente ela):
T-052 — "Modelar Entidades de Notificação e Outbox (NotificationMessage, OutboxMessage)"
  critérios/refs: AC-060 (Histórico pertence ao tenant), AC-061 (Outbox processada em background), AC-063 (Falha provoca retry com backoff)
  arquivos permitidos (e seus testes): src/Agendamento.Api/Domain/Notifications/NotificationMessage.cs, src/Agendamento.Api/Domain/Notifications/NotificationStatus.cs, src/Agendamento.Api/Domain/Notifications/NotificationChannel.cs, src/Agendamento.Api/Domain/Notifications/NotificationTemplate.cs
  mensagem de commit: "T-052 notificacoes-lembretes-whatsapp: Modelar Entidades de Notificação e Outbox (NotificationMessage, OutboxMessage)"

Regras inegociáveis:
- Todo critério de aceite referenciado vira teste com @spec:AC-xxx no título.
- NUNCA enfraqueça, pule (skip/todo) ou apague um teste para passar — teste pulado não é prova e o audit acusa.
- Rode os testes localmente com `powershell -ExecutionPolicy Bypass -File scripts/verify-spec.ps1` até passarem.
- NÃO edite tasks.md, NÃO rode onp-spec verify/audit e NÃO toque em outras tarefas — o orquestrador cuida disso.
- Ao final de CADA tarefa: `git add` só no que você tocou e um commit próprio.' 'gpt-5.6-terra' medium >> "$LOG_DIR/seq.log" 2>&1; then
    # commit de segurança se o agente esqueceu (rastreabilidade > perfeição)
    if [ -n "$(git status --porcelain)" ]; then
      git add -A && git commit -q -m 'T-052 notificacoes-lembretes-whatsapp: Modelar Entidades de Notificação e Outbox (NotificationMessage, OutboxMessage) (auto-commit do plano)'
    fi
    marcar_concluidas T-052
    verde "✔ T-052 concluída"
    return 0
  fi
  vermelho "✘ T-052 falhou (log: $LOG_DIR/seq.log)"
  amarelo "  reexecute só ela: bash .spec/features/notificacoes-lembretes-whatsapp/executar-tarefas.sh --seq T-052"
  FALHAS="$FALHAS T-052"
  return 1
}

# ── sequencial T-053 (ordem do tasks.md) ──
executar_seq_T_053() {
  info 'sequencial T-053 — Mapear Persistência EF Core e Outbox Pattern'
  if rodar_tarefa seq 'T-053' 'Você executa UMA tarefa da feature "notificacoes-lembretes-whatsapp" (fluxo onp-spec, spec-anchored).
Leia primeiro: .spec/features/notificacoes-lembretes-whatsapp/spec.md, .spec/features/notificacoes-lembretes-whatsapp/tasks.md e .spec/constituicao.md.

Sua tarefa (somente ela):
T-053 — "Mapear Persistência EF Core e Outbox Pattern"
  critérios/refs: AC-060 (Histórico pertence ao tenant), AC-061 (Outbox processada em background)
  arquivos permitidos (e seus testes): src/Agendamento.Api/Infrastructure/Persistence/AgendamentoDbContext.cs, src/Agendamento.Api/Infrastructure/Persistence/Configurations/NotificationMessageConfiguration.cs
  mensagem de commit: "T-053 notificacoes-lembretes-whatsapp: Mapear Persistência EF Core e Outbox Pattern"

Regras inegociáveis:
- Todo critério de aceite referenciado vira teste com @spec:AC-xxx no título.
- NUNCA enfraqueça, pule (skip/todo) ou apague um teste para passar — teste pulado não é prova e o audit acusa.
- Rode os testes localmente com `powershell -ExecutionPolicy Bypass -File scripts/verify-spec.ps1` até passarem.
- NÃO edite tasks.md, NÃO rode onp-spec verify/audit e NÃO toque em outras tarefas — o orquestrador cuida disso.
- Ao final de CADA tarefa: `git add` só no que você tocou e um commit próprio.' 'gpt-5.6-terra' medium >> "$LOG_DIR/seq.log" 2>&1; then
    # commit de segurança se o agente esqueceu (rastreabilidade > perfeição)
    if [ -n "$(git status --porcelain)" ]; then
      git add -A && git commit -q -m 'T-053 notificacoes-lembretes-whatsapp: Mapear Persistência EF Core e Outbox Pattern (auto-commit do plano)'
    fi
    marcar_concluidas T-053
    verde "✔ T-053 concluída"
    return 0
  fi
  vermelho "✘ T-053 falhou (log: $LOG_DIR/seq.log)"
  amarelo "  reexecute só ela: bash .spec/features/notificacoes-lembretes-whatsapp/executar-tarefas.sh --seq T-053"
  FALHAS="$FALHAS T-053"
  return 1
}

# ── sequencial T-054 (ordem do tasks.md) ──
executar_seq_T_054() {
  info 'sequencial T-054 — Implementar Provedor de Envio de Mensagens e Dispatcher Background'
  if rodar_tarefa seq 'T-054' 'Você executa UMA tarefa da feature "notificacoes-lembretes-whatsapp" (fluxo onp-spec, spec-anchored).
Leia primeiro: .spec/features/notificacoes-lembretes-whatsapp/spec.md, .spec/features/notificacoes-lembretes-whatsapp/tasks.md e .spec/constituicao.md.

Sua tarefa (somente ela):
T-054 — "Implementar Provedor de Envio de Mensagens e Dispatcher Background"
  critérios/refs: AC-061 (Outbox processada em background), AC-063 (Falha provoca retry com backoff)
  arquivos permitidos (e seus testes): src/Agendamento.Api/Infrastructure/Notifications/ICommunicationGateway.cs, src/Agendamento.Api/Infrastructure/Notifications/NotificationOutboxProcessor.cs
  mensagem de commit: "T-054 notificacoes-lembretes-whatsapp: Implementar Provedor de Envio de Mensagens e Dispatcher Background"

Regras inegociáveis:
- Todo critério de aceite referenciado vira teste com @spec:AC-xxx no título.
- NUNCA enfraqueça, pule (skip/todo) ou apague um teste para passar — teste pulado não é prova e o audit acusa.
- Rode os testes localmente com `powershell -ExecutionPolicy Bypass -File scripts/verify-spec.ps1` até passarem.
- NÃO edite tasks.md, NÃO rode onp-spec verify/audit e NÃO toque em outras tarefas — o orquestrador cuida disso.
- Ao final de CADA tarefa: `git add` só no que você tocou e um commit próprio.' 'gpt-5.6-terra' medium >> "$LOG_DIR/seq.log" 2>&1; then
    # commit de segurança se o agente esqueceu (rastreabilidade > perfeição)
    if [ -n "$(git status --porcelain)" ]; then
      git add -A && git commit -q -m 'T-054 notificacoes-lembretes-whatsapp: Implementar Provedor de Envio de Mensagens e Dispatcher Background (auto-commit do plano)'
    fi
    marcar_concluidas T-054
    verde "✔ T-054 concluída"
    return 0
  fi
  vermelho "✘ T-054 falhou (log: $LOG_DIR/seq.log)"
  amarelo "  reexecute só ela: bash .spec/features/notificacoes-lembretes-whatsapp/executar-tarefas.sh --seq T-054"
  FALHAS="$FALHAS T-054"
  return 1
}

# ── sequencial T-055 (ordem do tasks.md) ──
executar_seq_T_055() {
  info 'sequencial T-055 — Casos de Uso: Agendamento de Lembretes e Notificação de Orçamento'
  if rodar_tarefa seq 'T-055' 'Você executa UMA tarefa da feature "notificacoes-lembretes-whatsapp" (fluxo onp-spec, spec-anchored).
Leia primeiro: .spec/features/notificacoes-lembretes-whatsapp/spec.md, .spec/features/notificacoes-lembretes-whatsapp/tasks.md e .spec/constituicao.md.

Sua tarefa (somente ela):
T-055 — "Casos de Uso: Agendamento de Lembretes e Notificação de Orçamento"
  critérios/refs: AC-062 (Templates configuráveis por tenant)
  arquivos permitidos (e seus testes): src/Agendamento.Api/Application/Notifications/ScheduleAppointmentReminder/ScheduleAppointmentReminderCommand.cs, src/Agendamento.Api/Application/Notifications/ScheduleAppointmentReminder/ScheduleAppointmentReminderHandler.cs, src/Agendamento.Api/Application/Notifications/SendQuotationNotification/SendQuotationNotificationCommand.cs, src/Agendamento.Api/Application/Notifications/SendQuotationNotification/SendQuotationNotificationHandler.cs
  mensagem de commit: "T-055 notificacoes-lembretes-whatsapp: Casos de Uso: Agendamento de Lembretes e Notificação de Orçamento"

Regras inegociáveis:
- Todo critério de aceite referenciado vira teste com @spec:AC-xxx no título.
- NUNCA enfraqueça, pule (skip/todo) ou apague um teste para passar — teste pulado não é prova e o audit acusa.
- Rode os testes localmente com `powershell -ExecutionPolicy Bypass -File scripts/verify-spec.ps1` até passarem.
- NÃO edite tasks.md, NÃO rode onp-spec verify/audit e NÃO toque em outras tarefas — o orquestrador cuida disso.
- Ao final de CADA tarefa: `git add` só no que você tocou e um commit próprio.' 'gpt-5.6-terra' medium >> "$LOG_DIR/seq.log" 2>&1; then
    # commit de segurança se o agente esqueceu (rastreabilidade > perfeição)
    if [ -n "$(git status --porcelain)" ]; then
      git add -A && git commit -q -m 'T-055 notificacoes-lembretes-whatsapp: Casos de Uso: Agendamento de Lembretes e Notificação de Orçamento (auto-commit do plano)'
    fi
    marcar_concluidas T-055
    verde "✔ T-055 concluída"
    return 0
  fi
  vermelho "✘ T-055 falhou (log: $LOG_DIR/seq.log)"
  amarelo "  reexecute só ela: bash .spec/features/notificacoes-lembretes-whatsapp/executar-tarefas.sh --seq T-055"
  FALHAS="$FALHAS T-055"
  return 1
}

# ── sequencial T-056 (ordem do tasks.md) ──
executar_seq_T_056() {
  info 'sequencial T-056 — Expor Endpoints Minimal API de Notificações e Histórico'
  if rodar_tarefa seq 'T-056' 'Você executa UMA tarefa da feature "notificacoes-lembretes-whatsapp" (fluxo onp-spec, spec-anchored).
Leia primeiro: .spec/features/notificacoes-lembretes-whatsapp/spec.md, .spec/features/notificacoes-lembretes-whatsapp/tasks.md e .spec/constituicao.md.

Sua tarefa (somente ela):
T-056 — "Expor Endpoints Minimal API de Notificações e Histórico"
  critérios/refs: AC-060 (Histórico pertence ao tenant), AC-064 (Mensagem não expõe dados internos sensíveis)
  arquivos permitidos (e seus testes): src/Agendamento.Api/Features/Notifications/NotificationEndpoints.cs, src/Agendamento.Api/Program.cs
  mensagem de commit: "T-056 notificacoes-lembretes-whatsapp: Expor Endpoints Minimal API de Notificações e Histórico"

Regras inegociáveis:
- Todo critério de aceite referenciado vira teste com @spec:AC-xxx no título.
- NUNCA enfraqueça, pule (skip/todo) ou apague um teste para passar — teste pulado não é prova e o audit acusa.
- Rode os testes localmente com `powershell -ExecutionPolicy Bypass -File scripts/verify-spec.ps1` até passarem.
- NÃO edite tasks.md, NÃO rode onp-spec verify/audit e NÃO toque em outras tarefas — o orquestrador cuida disso.
- Ao final de CADA tarefa: `git add` só no que você tocou e um commit próprio.' 'gpt-5.6-terra' medium >> "$LOG_DIR/seq.log" 2>&1; then
    # commit de segurança se o agente esqueceu (rastreabilidade > perfeição)
    if [ -n "$(git status --porcelain)" ]; then
      git add -A && git commit -q -m 'T-056 notificacoes-lembretes-whatsapp: Expor Endpoints Minimal API de Notificações e Histórico (auto-commit do plano)'
    fi
    marcar_concluidas T-056
    verde "✔ T-056 concluída"
    return 0
  fi
  vermelho "✘ T-056 falhou (log: $LOG_DIR/seq.log)"
  amarelo "  reexecute só ela: bash .spec/features/notificacoes-lembretes-whatsapp/executar-tarefas.sh --seq T-056"
  FALHAS="$FALHAS T-056"
  return 1
}

# ── sequencial T-057 (ordem do tasks.md) ──
executar_seq_T_057() {
  info 'sequencial T-057 — Testes de Integração do Outbox Processor e Formatação de Mensagens'
  if rodar_tarefa seq 'T-057' 'Você executa UMA tarefa da feature "notificacoes-lembretes-whatsapp" (fluxo onp-spec, spec-anchored).
Leia primeiro: .spec/features/notificacoes-lembretes-whatsapp/spec.md, .spec/features/notificacoes-lembretes-whatsapp/tasks.md e .spec/constituicao.md.

Sua tarefa (somente ela):
T-057 — "Testes de Integração do Outbox Processor e Formatação de Mensagens"
  critérios/refs: AC-060 (Histórico pertence ao tenant), AC-061 (Outbox processada em background), AC-062 (Templates configuráveis por tenant), AC-063 (Falha provoca retry com backoff)
  arquivos permitidos (e seus testes): tests/Agendamento.UnitTests/Notifications/NotificationDomainTests.cs, tests/Agendamento.IntegrationTests/Notifications/NotificationApiTests.cs, src/Agendamento.Api/Infrastructure/Notifications/MockCommunicationGateway.cs
  mensagem de commit: "T-057 notificacoes-lembretes-whatsapp: Testes de Integração do Outbox Processor e Formatação de Mensagens"

Regras inegociáveis:
- Todo critério de aceite referenciado vira teste com @spec:AC-xxx no título.
- NUNCA enfraqueça, pule (skip/todo) ou apague um teste para passar — teste pulado não é prova e o audit acusa.
- Rode os testes localmente com `powershell -ExecutionPolicy Bypass -File scripts/verify-spec.ps1` até passarem.
- NÃO edite tasks.md, NÃO rode onp-spec verify/audit e NÃO toque em outras tarefas — o orquestrador cuida disso.
- Ao final de CADA tarefa: `git add` só no que você tocou e um commit próprio.' 'gpt-5.6-terra' medium >> "$LOG_DIR/seq.log" 2>&1; then
    # commit de segurança se o agente esqueceu (rastreabilidade > perfeição)
    if [ -n "$(git status --porcelain)" ]; then
      git add -A && git commit -q -m 'T-057 notificacoes-lembretes-whatsapp: Testes de Integração do Outbox Processor e Formatação de Mensagens (auto-commit do plano)'
    fi
    marcar_concluidas T-057
    verde "✔ T-057 concluída"
    return 0
  fi
  vermelho "✘ T-057 falhou (log: $LOG_DIR/seq.log)"
  amarelo "  reexecute só ela: bash .spec/features/notificacoes-lembretes-whatsapp/executar-tarefas.sh --seq T-057"
  FALHAS="$FALHAS T-057"
  return 1
}

# ── gate: quem decide é a máquina ────────────────────────────────────
rodar_gate() {
  echo
  info "gate: verify + audit --ci"
  evento --tipo gate --etapa inicio
  node "$ENGINE" verify "$FEATURE"
  local v=$?
  evento --tipo gate --etapa verify --exit "$v"
  node "$ENGINE" audit --ci
  AUDIT=$?
  evento --tipo gate --etapa audit --exit "$AUDIT"
  # fecha a contabilidade: status das tarefas + prova do verify no git
  if [ -n "$(git status --porcelain -- '.spec')" ]; then
    git add -A -- '.spec'
    git commit -q -m "$FEATURE: status das tarefas + prova do verify (plano)"
    info "status das tarefas e prova do verify commitados"
  fi
  return "$AUDIT"
}

encerrar() { # $1=escopo
  echo
  if [ -n "$FALHAS" ]; then vermelho "faixas/tarefas com falha:$FALHAS"; fi
  # sem gate não existe veredito: NUNCA anunciar alinhamento sem o audit
  if [ "$COM_GATE" -eq 0 ]; then
    evento --tipo fim --exit 1 --escopo "$1"
    if [ -z "$FALHAS" ]; then
      amarelo "○ trabalho de '$1' terminou SEM o gate (--sem-gate) — isto NÃO é prova de nada"
      amarelo "  para o veredito: bash .spec/features/notificacoes-lembretes-whatsapp/executar-tarefas.sh --gate"
      exit 0
    fi
    vermelho "e ainda há falhas — conserte e rode o gate"
    exit 1
  fi
  rodar_gate
  local audit=$?
  if [ "$audit" -eq 0 ] && [ -z "$FALHAS" ]; then
    evento --tipo fim --exit 0 --escopo "$1"
    verde "✔ plano concluído — especificação e código alinhados (audit exit 0) na branch $BASE_BRANCH"
    info "próximo passo: revise e leve para a main quando quiser (git merge $BASE_BRANCH)"
    exit 0
  fi
  evento --tipo fim --exit 1 --escopo "$1"
  vermelho "plano terminou com pendências — leia a saída do audit acima e os logs em $LOG_DIR"
  amarelo "dica: reexecute só o que falhou (--faixa <id> / --seq <T-xxx>)"
  exit 1
}

executar_tudo() {
  evento --tipo inicio --escopo tudo
  iniciar_resumos
  info "logs em: $LOG_DIR"
  info "resumo geral de andamento: a cada 1 min aqui no terminal (e via: onp-spec resumo)"
  executar_seq_T_052 || true
  executar_seq_T_053 || true
  executar_seq_T_054 || true
  executar_seq_T_055 || true
  executar_seq_T_056 || true
  executar_seq_T_057 || true
  encerrar tudo
}

listar() {
  echo "execução: $RUN_ID (feature $FEATURE, branch $BASE_BRANCH)"
  echo "  seq       T-052 (sequencial)"
  echo "  seq       T-053 (sequencial)"
  echo "  seq       T-054 (sequencial)"
  echo "  seq       T-055 (sequencial)"
  echo "  seq       T-056 (sequencial)"
  echo "  seq       T-057 (sequencial)"
  echo
  echo "reexecutar uma faixa:    --faixa <id>"
  echo "reexecutar sequencial:   --seq <T-xxx>"
  echo "só o gate:               --gate"
}

MODO="tudo"
ALVO=""
while [ $# -gt 0 ]; do
  case "$1" in
    --listar) MODO="listar" ;;
    --gate) MODO="gate" ;;
    --sem-gate) COM_GATE=0 ;;
    --faixa) MODO="faixa"; ALVO="${2:-}"; shift ;;
    --seq) MODO="seq"; ALVO="${2:-}"; shift ;;
    -h|--help) sed -n "2,14p" "$0"; exit 0 ;;
    *) vermelho "argumento desconhecido: $1"; sed -n "2,14p" "$0"; exit 2 ;;
  esac
  shift
done

if [ "$MODO" = "listar" ]; then listar; exit 0; fi

preparar_ambiente

case "$MODO" in
  tudo) executar_tudo ;;
  gate) COM_GATE=1; iniciar_resumos; encerrar gate ;;
  faixa)
    case "$ALVO" in
      *) falhar "faixa desconhecida: '$ALVO' — veja as disponíveis com --listar" ;;
    esac ;;
  seq)
    case "$ALVO" in
      T-052) evento --tipo inicio --escopo "seq:T-052"; iniciar_resumos; executar_seq_T_052 || true; encerrar "seq:T-052" ;;
      T-053) evento --tipo inicio --escopo "seq:T-053"; iniciar_resumos; executar_seq_T_053 || true; encerrar "seq:T-053" ;;
      T-054) evento --tipo inicio --escopo "seq:T-054"; iniciar_resumos; executar_seq_T_054 || true; encerrar "seq:T-054" ;;
      T-055) evento --tipo inicio --escopo "seq:T-055"; iniciar_resumos; executar_seq_T_055 || true; encerrar "seq:T-055" ;;
      T-056) evento --tipo inicio --escopo "seq:T-056"; iniciar_resumos; executar_seq_T_056 || true; encerrar "seq:T-056" ;;
      T-057) evento --tipo inicio --escopo "seq:T-057"; iniciar_resumos; executar_seq_T_057 || true; encerrar "seq:T-057" ;;
      *) falhar "tarefa sequencial desconhecida: '$ALVO' — veja as disponíveis com --listar" ;;
    esac ;;
esac
