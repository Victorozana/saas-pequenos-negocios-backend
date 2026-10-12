#!/usr/bin/env bash
# executar-tarefas.sh — gerado por `onp-spec plano gestao-financeira-contas` em 2026-10-12 03:55
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
# resumo do que está rolando, a qualquer momento: onp-spec resumo gestao-financeira-contas
set -u
set -o pipefail

RUN_ID='saas-pequenos-negocios-backend-gestao-financeira-contas-mv4pyu2g'
FEATURE='gestao-financeira-contas'
BASE_BRANCH='spec/gestao-financeira-contas'
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
  git ls-files --error-unmatch -- '.spec/features/gestao-financeira-contas/spec.md' >/dev/null 2>&1 || falhar "spec.md não está commitada — os worktrees das faixas precisam dela no git"
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
  LOG_DIR="$(dirname "$TOPLEVEL")/onp-worktrees/saas-pequenos-negocios-backend-gestao-financeira-contas-logs"
  WT_BASE="$(dirname "$TOPLEVEL")/onp-worktrees/saas-pequenos-negocios-backend-gestao-financeira-contas"
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
    amarelo "  reexecute só ela: bash .spec/features/gestao-financeira-contas/executar-tarefas.sh --faixa $1"
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

# ── sequencial T-045 (ordem do tasks.md) ──
executar_seq_T_045() {
  info 'sequencial T-045 — Modelar Entidades Financeiras (FinancialTransaction, Receivable, Payable)'
  if rodar_tarefa seq 'T-045' 'Você executa UMA tarefa da feature "gestao-financeira-contas" (fluxo onp-spec, spec-anchored).
Leia primeiro: .spec/features/gestao-financeira-contas/spec.md, .spec/features/gestao-financeira-contas/tasks.md e .spec/constituicao.md.

Sua tarefa (somente ela):
T-045 — "Modelar Entidades Financeiras (FinancialTransaction, Receivable, Payable)"
  critérios/refs: AC-055 (Registros financeiros pertencem ao tenant), AC-057 (Baixa parcial acumula pagamentos até quitação), AC-058 (Títulos vencidos são sinalizados)
  arquivos permitidos (e seus testes): src/Agendamento.Api/Domain/Financial/ReceivableTitle.cs, src/Agendamento.Api/Domain/Financial/PayableTitle.cs, src/Agendamento.Api/Domain/Financial/PaymentEntry.cs, src/Agendamento.Api/Domain/Financial/TransactionStatus.cs, src/Agendamento.Api/Domain/Financial/PaymentMethod.cs
  mensagem de commit: "T-045 gestao-financeira-contas: Modelar Entidades Financeiras (FinancialTransaction, Receivable, Payable)"

Regras inegociáveis:
- Todo critério de aceite referenciado vira teste com @spec:AC-xxx no título.
- NUNCA enfraqueça, pule (skip/todo) ou apague um teste para passar — teste pulado não é prova e o audit acusa.
- Rode os testes localmente com `powershell -ExecutionPolicy Bypass -File scripts/verify-spec.ps1` até passarem.
- NÃO edite tasks.md, NÃO rode onp-spec verify/audit e NÃO toque em outras tarefas — o orquestrador cuida disso.
- Ao final de CADA tarefa: `git add` só no que você tocou e um commit próprio.' 'gpt-5.6-terra' medium >> "$LOG_DIR/seq.log" 2>&1; then
    # commit de segurança se o agente esqueceu (rastreabilidade > perfeição)
    if [ -n "$(git status --porcelain)" ]; then
      git add -A && git commit -q -m 'T-045 gestao-financeira-contas: Modelar Entidades Financeiras (FinancialTransaction, Receivable, Payable) (auto-commit do plano)'
    fi
    marcar_concluidas T-045
    verde "✔ T-045 concluída"
    return 0
  fi
  vermelho "✘ T-045 falhou (log: $LOG_DIR/seq.log)"
  amarelo "  reexecute só ela: bash .spec/features/gestao-financeira-contas/executar-tarefas.sh --seq T-045"
  FALHAS="$FALHAS T-045"
  return 1
}

# ── sequencial T-046 (ordem do tasks.md) ──
executar_seq_T_046() {
  info 'sequencial T-046 — Configurar Persistência EF Core e Migrations Financeiras'
  if rodar_tarefa seq 'T-046' 'Você executa UMA tarefa da feature "gestao-financeira-contas" (fluxo onp-spec, spec-anchored).
Leia primeiro: .spec/features/gestao-financeira-contas/spec.md, .spec/features/gestao-financeira-contas/tasks.md e .spec/constituicao.md.

Sua tarefa (somente ela):
T-046 — "Configurar Persistência EF Core e Migrations Financeiras"
  critérios/refs: AC-055 (Registros financeiros pertencem ao tenant)
  arquivos permitidos (e seus testes): src/Agendamento.Api/Infrastructure/Persistence/AgendamentoDbContext.cs, src/Agendamento.Api/Infrastructure/Persistence/Configurations/ReceivableTitleConfiguration.cs, src/Agendamento.Api/Infrastructure/Persistence/Configurations/PayableTitleConfiguration.cs
  mensagem de commit: "T-046 gestao-financeira-contas: Configurar Persistência EF Core e Migrations Financeiras"

Regras inegociáveis:
- Todo critério de aceite referenciado vira teste com @spec:AC-xxx no título.
- NUNCA enfraqueça, pule (skip/todo) ou apague um teste para passar — teste pulado não é prova e o audit acusa.
- Rode os testes localmente com `powershell -ExecutionPolicy Bypass -File scripts/verify-spec.ps1` até passarem.
- NÃO edite tasks.md, NÃO rode onp-spec verify/audit e NÃO toque em outras tarefas — o orquestrador cuida disso.
- Ao final de CADA tarefa: `git add` só no que você tocou e um commit próprio.' 'gpt-5.6-terra' medium >> "$LOG_DIR/seq.log" 2>&1; then
    # commit de segurança se o agente esqueceu (rastreabilidade > perfeição)
    if [ -n "$(git status --porcelain)" ]; then
      git add -A && git commit -q -m 'T-046 gestao-financeira-contas: Configurar Persistência EF Core e Migrations Financeiras (auto-commit do plano)'
    fi
    marcar_concluidas T-046
    verde "✔ T-046 concluída"
    return 0
  fi
  vermelho "✘ T-046 falhou (log: $LOG_DIR/seq.log)"
  amarelo "  reexecute só ela: bash .spec/features/gestao-financeira-contas/executar-tarefas.sh --seq T-046"
  FALHAS="$FALHAS T-046"
  return 1
}

# ── sequencial T-047 (ordem do tasks.md) ──
executar_seq_T_047() {
  info 'sequencial T-047 — Caso de Uso: Lançamento Automático de Títulos via Orçamento/OS'
  if rodar_tarefa seq 'T-047' 'Você executa UMA tarefa da feature "gestao-financeira-contas" (fluxo onp-spec, spec-anchored).
Leia primeiro: .spec/features/gestao-financeira-contas/spec.md, .spec/features/gestao-financeira-contas/tasks.md e .spec/constituicao.md.

Sua tarefa (somente ela):
T-047 — "Caso de Uso: Lançamento Automático de Títulos via Orçamento/OS"
  critérios/refs: AC-056 (Entrada gera título no valor configurado)
  arquivos permitidos (e seus testes): src/Agendamento.Api/Application/Financial/GenerateReceivablesFromQuotation/GenerateReceivablesFromQuotationHandler.cs
  mensagem de commit: "T-047 gestao-financeira-contas: Caso de Uso: Lançamento Automático de Títulos via Orçamento/OS"

Regras inegociáveis:
- Todo critério de aceite referenciado vira teste com @spec:AC-xxx no título.
- NUNCA enfraqueça, pule (skip/todo) ou apague um teste para passar — teste pulado não é prova e o audit acusa.
- Rode os testes localmente com `powershell -ExecutionPolicy Bypass -File scripts/verify-spec.ps1` até passarem.
- NÃO edite tasks.md, NÃO rode onp-spec verify/audit e NÃO toque em outras tarefas — o orquestrador cuida disso.
- Ao final de CADA tarefa: `git add` só no que você tocou e um commit próprio.' 'gpt-5.6-terra' medium >> "$LOG_DIR/seq.log" 2>&1; then
    # commit de segurança se o agente esqueceu (rastreabilidade > perfeição)
    if [ -n "$(git status --porcelain)" ]; then
      git add -A && git commit -q -m 'T-047 gestao-financeira-contas: Caso de Uso: Lançamento Automático de Títulos via Orçamento/OS (auto-commit do plano)'
    fi
    marcar_concluidas T-047
    verde "✔ T-047 concluída"
    return 0
  fi
  vermelho "✘ T-047 falhou (log: $LOG_DIR/seq.log)"
  amarelo "  reexecute só ela: bash .spec/features/gestao-financeira-contas/executar-tarefas.sh --seq T-047"
  FALHAS="$FALHAS T-047"
  return 1
}

# ── sequencial T-048 (ordem do tasks.md) ──
executar_seq_T_048() {
  info 'sequencial T-048 — Caso de Uso: Registro de Liquidação / Baixa de Títulos'
  if rodar_tarefa seq 'T-048' 'Você executa UMA tarefa da feature "gestao-financeira-contas" (fluxo onp-spec, spec-anchored).
Leia primeiro: .spec/features/gestao-financeira-contas/spec.md, .spec/features/gestao-financeira-contas/tasks.md e .spec/constituicao.md.

Sua tarefa (somente ela):
T-048 — "Caso de Uso: Registro de Liquidação / Baixa de Títulos"
  critérios/refs: AC-057 (Baixa parcial acumula pagamentos até quitação)
  arquivos permitidos (e seus testes): src/Agendamento.Api/Application/Financial/RegisterPayment/RegisterPaymentHandler.cs
  mensagem de commit: "T-048 gestao-financeira-contas: Caso de Uso: Registro de Liquidação / Baixa de Títulos"

Regras inegociáveis:
- Todo critério de aceite referenciado vira teste com @spec:AC-xxx no título.
- NUNCA enfraqueça, pule (skip/todo) ou apague um teste para passar — teste pulado não é prova e o audit acusa.
- Rode os testes localmente com `powershell -ExecutionPolicy Bypass -File scripts/verify-spec.ps1` até passarem.
- NÃO edite tasks.md, NÃO rode onp-spec verify/audit e NÃO toque em outras tarefas — o orquestrador cuida disso.
- Ao final de CADA tarefa: `git add` só no que você tocou e um commit próprio.' 'gpt-5.6-terra' medium >> "$LOG_DIR/seq.log" 2>&1; then
    # commit de segurança se o agente esqueceu (rastreabilidade > perfeição)
    if [ -n "$(git status --porcelain)" ]; then
      git add -A && git commit -q -m 'T-048 gestao-financeira-contas: Caso de Uso: Registro de Liquidação / Baixa de Títulos (auto-commit do plano)'
    fi
    marcar_concluidas T-048
    verde "✔ T-048 concluída"
    return 0
  fi
  vermelho "✘ T-048 falhou (log: $LOG_DIR/seq.log)"
  amarelo "  reexecute só ela: bash .spec/features/gestao-financeira-contas/executar-tarefas.sh --seq T-048"
  FALHAS="$FALHAS T-048"
  return 1
}

# ── sequencial T-049 (ordem do tasks.md) ──
executar_seq_T_049() {
  info 'sequencial T-049 — Casos de Uso: Gestão de Contas a Pagar e Extrato Financeiro'
  if rodar_tarefa seq 'T-049' 'Você executa UMA tarefa da feature "gestao-financeira-contas" (fluxo onp-spec, spec-anchored).
Leia primeiro: .spec/features/gestao-financeira-contas/spec.md, .spec/features/gestao-financeira-contas/tasks.md e .spec/constituicao.md.

Sua tarefa (somente ela):
T-049 — "Casos de Uso: Gestão de Contas a Pagar e Extrato Financeiro"
  critérios/refs: AC-058 (Títulos vencidos são sinalizados), AC-059 (Extrato consolida totais por período)
  arquivos permitidos (e seus testes): src/Agendamento.Api/Application/Financial/CreatePayable/CreatePayableHandler.cs, src/Agendamento.Api/Application/Financial/GetFinancialStatement/GetFinancialStatementHandler.cs
  mensagem de commit: "T-049 gestao-financeira-contas: Casos de Uso: Gestão de Contas a Pagar e Extrato Financeiro"

Regras inegociáveis:
- Todo critério de aceite referenciado vira teste com @spec:AC-xxx no título.
- NUNCA enfraqueça, pule (skip/todo) ou apague um teste para passar — teste pulado não é prova e o audit acusa.
- Rode os testes localmente com `powershell -ExecutionPolicy Bypass -File scripts/verify-spec.ps1` até passarem.
- NÃO edite tasks.md, NÃO rode onp-spec verify/audit e NÃO toque em outras tarefas — o orquestrador cuida disso.
- Ao final de CADA tarefa: `git add` só no que você tocou e um commit próprio.' 'gpt-5.6-terra' medium >> "$LOG_DIR/seq.log" 2>&1; then
    # commit de segurança se o agente esqueceu (rastreabilidade > perfeição)
    if [ -n "$(git status --porcelain)" ]; then
      git add -A && git commit -q -m 'T-049 gestao-financeira-contas: Casos de Uso: Gestão de Contas a Pagar e Extrato Financeiro (auto-commit do plano)'
    fi
    marcar_concluidas T-049
    verde "✔ T-049 concluída"
    return 0
  fi
  vermelho "✘ T-049 falhou (log: $LOG_DIR/seq.log)"
  amarelo "  reexecute só ela: bash .spec/features/gestao-financeira-contas/executar-tarefas.sh --seq T-049"
  FALHAS="$FALHAS T-049"
  return 1
}

# ── sequencial T-050 (ordem do tasks.md) ──
executar_seq_T_050() {
  info 'sequencial T-050 — Expor Endpoints Minimal API Financeiros'
  if rodar_tarefa seq 'T-050' 'Você executa UMA tarefa da feature "gestao-financeira-contas" (fluxo onp-spec, spec-anchored).
Leia primeiro: .spec/features/gestao-financeira-contas/spec.md, .spec/features/gestao-financeira-contas/tasks.md e .spec/constituicao.md.

Sua tarefa (somente ela):
T-050 — "Expor Endpoints Minimal API Financeiros"
  critérios/refs: AC-055 (Registros financeiros pertencem ao tenant), AC-059 (Extrato consolida totais por período)
  arquivos permitidos (e seus testes): src/Agendamento.Api/Features/Financial/FinancialEndpoints.cs, src/Agendamento.Api/Program.cs
  mensagem de commit: "T-050 gestao-financeira-contas: Expor Endpoints Minimal API Financeiros"

Regras inegociáveis:
- Todo critério de aceite referenciado vira teste com @spec:AC-xxx no título.
- NUNCA enfraqueça, pule (skip/todo) ou apague um teste para passar — teste pulado não é prova e o audit acusa.
- Rode os testes localmente com `powershell -ExecutionPolicy Bypass -File scripts/verify-spec.ps1` até passarem.
- NÃO edite tasks.md, NÃO rode onp-spec verify/audit e NÃO toque em outras tarefas — o orquestrador cuida disso.
- Ao final de CADA tarefa: `git add` só no que você tocou e um commit próprio.' 'gpt-5.6-terra' medium >> "$LOG_DIR/seq.log" 2>&1; then
    # commit de segurança se o agente esqueceu (rastreabilidade > perfeição)
    if [ -n "$(git status --porcelain)" ]; then
      git add -A && git commit -q -m 'T-050 gestao-financeira-contas: Expor Endpoints Minimal API Financeiros (auto-commit do plano)'
    fi
    marcar_concluidas T-050
    verde "✔ T-050 concluída"
    return 0
  fi
  vermelho "✘ T-050 falhou (log: $LOG_DIR/seq.log)"
  amarelo "  reexecute só ela: bash .spec/features/gestao-financeira-contas/executar-tarefas.sh --seq T-050"
  FALHAS="$FALHAS T-050"
  return 1
}

# ── sequencial T-051 (ordem do tasks.md) ──
executar_seq_T_051() {
  info 'sequencial T-051 — Testes de Integração Financeira, Baixas Parciais e Isolamento RLS'
  if rodar_tarefa seq 'T-051' 'Você executa UMA tarefa da feature "gestao-financeira-contas" (fluxo onp-spec, spec-anchored).
Leia primeiro: .spec/features/gestao-financeira-contas/spec.md, .spec/features/gestao-financeira-contas/tasks.md e .spec/constituicao.md.

Sua tarefa (somente ela):
T-051 — "Testes de Integração Financeira, Baixas Parciais e Isolamento RLS"
  critérios/refs: AC-055 (Registros financeiros pertencem ao tenant), AC-056 (Entrada gera título no valor configurado), AC-057 (Baixa parcial acumula pagamentos até quitação), AC-058 (Títulos vencidos são sinalizados), AC-059 (Extrato consolida totais por período)
  arquivos permitidos (e seus testes): tests/Agendamento.UnitTests/Financial/FinancialDomainTests.cs, tests/Agendamento.IntegrationTests/Financial/FinancialApiTests.cs, tests/Agendamento.IntegrationTests/Financial/FinancialIsolationTests.cs
  mensagem de commit: "T-051 gestao-financeira-contas: Testes de Integração Financeira, Baixas Parciais e Isolamento RLS"

Regras inegociáveis:
- Todo critério de aceite referenciado vira teste com @spec:AC-xxx no título.
- NUNCA enfraqueça, pule (skip/todo) ou apague um teste para passar — teste pulado não é prova e o audit acusa.
- Rode os testes localmente com `powershell -ExecutionPolicy Bypass -File scripts/verify-spec.ps1` até passarem.
- NÃO edite tasks.md, NÃO rode onp-spec verify/audit e NÃO toque em outras tarefas — o orquestrador cuida disso.
- Ao final de CADA tarefa: `git add` só no que você tocou e um commit próprio.' 'gpt-5.6-terra' medium >> "$LOG_DIR/seq.log" 2>&1; then
    # commit de segurança se o agente esqueceu (rastreabilidade > perfeição)
    if [ -n "$(git status --porcelain)" ]; then
      git add -A && git commit -q -m 'T-051 gestao-financeira-contas: Testes de Integração Financeira, Baixas Parciais e Isolamento RLS (auto-commit do plano)'
    fi
    marcar_concluidas T-051
    verde "✔ T-051 concluída"
    return 0
  fi
  vermelho "✘ T-051 falhou (log: $LOG_DIR/seq.log)"
  amarelo "  reexecute só ela: bash .spec/features/gestao-financeira-contas/executar-tarefas.sh --seq T-051"
  FALHAS="$FALHAS T-051"
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
      amarelo "  para o veredito: bash .spec/features/gestao-financeira-contas/executar-tarefas.sh --gate"
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
  executar_seq_T_045 || true
  executar_seq_T_046 || true
  executar_seq_T_047 || true
  executar_seq_T_048 || true
  executar_seq_T_049 || true
  executar_seq_T_050 || true
  executar_seq_T_051 || true
  encerrar tudo
}

listar() {
  echo "execução: $RUN_ID (feature $FEATURE, branch $BASE_BRANCH)"
  echo "  seq       T-045 (sequencial)"
  echo "  seq       T-046 (sequencial)"
  echo "  seq       T-047 (sequencial)"
  echo "  seq       T-048 (sequencial)"
  echo "  seq       T-049 (sequencial)"
  echo "  seq       T-050 (sequencial)"
  echo "  seq       T-051 (sequencial)"
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
      T-045) evento --tipo inicio --escopo "seq:T-045"; iniciar_resumos; executar_seq_T_045 || true; encerrar "seq:T-045" ;;
      T-046) evento --tipo inicio --escopo "seq:T-046"; iniciar_resumos; executar_seq_T_046 || true; encerrar "seq:T-046" ;;
      T-047) evento --tipo inicio --escopo "seq:T-047"; iniciar_resumos; executar_seq_T_047 || true; encerrar "seq:T-047" ;;
      T-048) evento --tipo inicio --escopo "seq:T-048"; iniciar_resumos; executar_seq_T_048 || true; encerrar "seq:T-048" ;;
      T-049) evento --tipo inicio --escopo "seq:T-049"; iniciar_resumos; executar_seq_T_049 || true; encerrar "seq:T-049" ;;
      T-050) evento --tipo inicio --escopo "seq:T-050"; iniciar_resumos; executar_seq_T_050 || true; encerrar "seq:T-050" ;;
      T-051) evento --tipo inicio --escopo "seq:T-051"; iniciar_resumos; executar_seq_T_051 || true; encerrar "seq:T-051" ;;
      *) falhar "tarefa sequencial desconhecida: '$ALVO' — veja as disponíveis com --listar" ;;
    esac ;;
esac
