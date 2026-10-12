#!/usr/bin/env bash
# executar-tarefas.sh — gerado por `onp-spec plano gestao-equipe-permissoes` em 2026-10-12 00:26
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
# resumo do que está rolando, a qualquer momento: onp-spec resumo gestao-equipe-permissoes
set -u
set -o pipefail

RUN_ID='saas-pequenos-negocios-backend-gestao-equipe-permissoes-mv4ihb3h'
FEATURE='gestao-equipe-permissoes'
BASE_BRANCH='spec/gestao-equipe-permissoes'
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
  git ls-files --error-unmatch -- '.spec/features/gestao-equipe-permissoes/spec.md' >/dev/null 2>&1 || falhar "spec.md não está commitada — os worktrees das faixas precisam dela no git"
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
  LOG_DIR="$(dirname "$TOPLEVEL")/onp-worktrees/saas-pequenos-negocios-backend-gestao-equipe-permissoes-logs"
  WT_BASE="$(dirname "$TOPLEVEL")/onp-worktrees/saas-pequenos-negocios-backend-gestao-equipe-permissoes"
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
    amarelo "  reexecute só ela: bash .spec/features/gestao-equipe-permissoes/executar-tarefas.sh --faixa $1"
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

# ── sequencial T-080 (ordem do tasks.md) ──
executar_seq_T_080() {
  info 'sequencial T-080 — Atualizar entidade User com nome e DTO Profile'
  if rodar_tarefa seq 'T-080' 'Você executa UMA tarefa da feature "gestao-equipe-permissoes" (fluxo onp-spec, spec-anchored).
Leia primeiro: .spec/features/gestao-equipe-permissoes/spec.md, .spec/features/gestao-equipe-permissoes/tasks.md e .spec/constituicao.md.

Sua tarefa (somente ela):
T-080 — "Atualizar entidade User com nome e DTO Profile"
  critérios/refs: AC-070 (Perfil completo do usuário)
  arquivos permitidos (e seus testes): src/Agendamento.Api/Domain/Identity/User.cs, src/Agendamento.Api/Application/Tenants/RegisterTenant/RegisterTenantHandler.cs, src/Agendamento.Api/Features/Identity/CurrentUserEndpoints.cs, src/Agendamento.Api/Application/Identity/GetUserProfile/UserProfileResponse.cs
  mensagem de commit: "T-080 gestao-equipe-permissoes: Atualizar entidade User com nome e DTO Profile"

Regras inegociáveis:
- Todo critério de aceite referenciado vira teste com @spec:AC-xxx no título.
- NUNCA enfraqueça, pule (skip/todo) ou apague um teste para passar — teste pulado não é prova e o audit acusa.
- Rode os testes localmente com `powershell -ExecutionPolicy Bypass -File scripts/verify-spec.ps1` até passarem.
- NÃO edite tasks.md, NÃO rode onp-spec verify/audit e NÃO toque em outras tarefas — o orquestrador cuida disso.
- Ao final de CADA tarefa: `git add` só no que você tocou e um commit próprio.' 'gpt-5.6-terra' medium >> "$LOG_DIR/seq.log" 2>&1; then
    # commit de segurança se o agente esqueceu (rastreabilidade > perfeição)
    if [ -n "$(git status --porcelain)" ]; then
      git add -A && git commit -q -m 'T-080 gestao-equipe-permissoes: Atualizar entidade User com nome e DTO Profile (auto-commit do plano)'
    fi
    marcar_concluidas T-080
    verde "✔ T-080 concluída"
    return 0
  fi
  vermelho "✘ T-080 falhou (log: $LOG_DIR/seq.log)"
  amarelo "  reexecute só ela: bash .spec/features/gestao-equipe-permissoes/executar-tarefas.sh --seq T-080"
  FALHAS="$FALHAS T-080"
  return 1
}

# ── sequencial T-081 (ordem do tasks.md) ──
executar_seq_T_081() {
  info 'sequencial T-081 — Definir Permissions e autorização em TenantMembership'
  if rodar_tarefa seq 'T-081' 'Você executa UMA tarefa da feature "gestao-equipe-permissoes" (fluxo onp-spec, spec-anchored).
Leia primeiro: .spec/features/gestao-equipe-permissoes/spec.md, .spec/features/gestao-equipe-permissoes/tasks.md e .spec/constituicao.md.

Sua tarefa (somente ela):
T-081 — "Definir Permissions e autorização em TenantMembership"
  critérios/refs: AC-070 (Perfil completo do usuário), AC-071 (Bloqueio de acesso por falta de permissão)
  arquivos permitidos (e seus testes): src/Agendamento.Api/Domain/Identity/Permissions.cs, src/Agendamento.Api/Domain/Tenants/TenantMembership.cs, src/Agendamento.Api/Infrastructure/Persistence/Configurations/TenantMembershipConfiguration.cs
  mensagem de commit: "T-081 gestao-equipe-permissoes: Definir Permissions e autorização em TenantMembership"

Regras inegociáveis:
- Todo critério de aceite referenciado vira teste com @spec:AC-xxx no título.
- NUNCA enfraqueça, pule (skip/todo) ou apague um teste para passar — teste pulado não é prova e o audit acusa.
- Rode os testes localmente com `powershell -ExecutionPolicy Bypass -File scripts/verify-spec.ps1` até passarem.
- NÃO edite tasks.md, NÃO rode onp-spec verify/audit e NÃO toque em outras tarefas — o orquestrador cuida disso.
- Ao final de CADA tarefa: `git add` só no que você tocou e um commit próprio.' 'gpt-5.6-terra' medium >> "$LOG_DIR/seq.log" 2>&1; then
    # commit de segurança se o agente esqueceu (rastreabilidade > perfeição)
    if [ -n "$(git status --porcelain)" ]; then
      git add -A && git commit -q -m 'T-081 gestao-equipe-permissoes: Definir Permissions e autorização em TenantMembership (auto-commit do plano)'
    fi
    marcar_concluidas T-081
    verde "✔ T-081 concluída"
    return 0
  fi
  vermelho "✘ T-081 falhou (log: $LOG_DIR/seq.log)"
  amarelo "  reexecute só ela: bash .spec/features/gestao-equipe-permissoes/executar-tarefas.sh --seq T-081"
  FALHAS="$FALHAS T-081"
  return 1
}

# ── sequencial T-082 (ordem do tasks.md) ──
executar_seq_T_082() {
  info 'sequencial T-082 — Middleware/Handler de Permissões'
  if rodar_tarefa seq 'T-082' 'Você executa UMA tarefa da feature "gestao-equipe-permissoes" (fluxo onp-spec, spec-anchored).
Leia primeiro: .spec/features/gestao-equipe-permissoes/spec.md, .spec/features/gestao-equipe-permissoes/tasks.md e .spec/constituicao.md.

Sua tarefa (somente ela):
T-082 — "Middleware/Handler de Permissões"
  critérios/refs: AC-071 (Bloqueio de acesso por falta de permissão)
  arquivos permitidos (e seus testes): src/Agendamento.Api/Infrastructure/Authorization/PermissionAuthorizationExtensions.cs, src/Agendamento.Api/Infrastructure/DependencyInjection.cs, src/Agendamento.Api/Infrastructure/Identity/EfIdentityStore.cs
  mensagem de commit: "T-082 gestao-equipe-permissoes: Middleware/Handler de Permissões"

Regras inegociáveis:
- Todo critério de aceite referenciado vira teste com @spec:AC-xxx no título.
- NUNCA enfraqueça, pule (skip/todo) ou apague um teste para passar — teste pulado não é prova e o audit acusa.
- Rode os testes localmente com `powershell -ExecutionPolicy Bypass -File scripts/verify-spec.ps1` até passarem.
- NÃO edite tasks.md, NÃO rode onp-spec verify/audit e NÃO toque em outras tarefas — o orquestrador cuida disso.
- Ao final de CADA tarefa: `git add` só no que você tocou e um commit próprio.' 'gpt-5.6-terra' medium >> "$LOG_DIR/seq.log" 2>&1; then
    # commit de segurança se o agente esqueceu (rastreabilidade > perfeição)
    if [ -n "$(git status --porcelain)" ]; then
      git add -A && git commit -q -m 'T-082 gestao-equipe-permissoes: Middleware/Handler de Permissões (auto-commit do plano)'
    fi
    marcar_concluidas T-082
    verde "✔ T-082 concluída"
    return 0
  fi
  vermelho "✘ T-082 falhou (log: $LOG_DIR/seq.log)"
  amarelo "  reexecute só ela: bash .spec/features/gestao-equipe-permissoes/executar-tarefas.sh --seq T-082"
  FALHAS="$FALHAS T-082"
  return 1
}

# ── sequencial T-083 (ordem do tasks.md) ──
executar_seq_T_083() {
  info 'sequencial T-083 — Aplicar RequirePermission nos endpoints'
  if rodar_tarefa seq 'T-083' 'Você executa UMA tarefa da feature "gestao-equipe-permissoes" (fluxo onp-spec, spec-anchored).
Leia primeiro: .spec/features/gestao-equipe-permissoes/spec.md, .spec/features/gestao-equipe-permissoes/tasks.md e .spec/constituicao.md.

Sua tarefa (somente ela):
T-083 — "Aplicar RequirePermission nos endpoints"
  critérios/refs: AC-071 (Bloqueio de acesso por falta de permissão)
  arquivos permitidos (e seus testes): src/Agendamento.Api/Features/Customers/CustomerEndpoints.cs, src/Agendamento.Api/Features/Services/ServiceEndpoints.cs, src/Agendamento.Api/Features/Quotations/QuotationEndpoints.cs, src/Agendamento.Api/Features/Appointments/AppointmentEndpoints.cs, src/Agendamento.Api/Features/Dashboard/DashboardEndpoints.cs, src/Agendamento.Api/Features/Financial/FinancialEndpoints.cs
  mensagem de commit: "T-083 gestao-equipe-permissoes: Aplicar RequirePermission nos endpoints"

Regras inegociáveis:
- Todo critério de aceite referenciado vira teste com @spec:AC-xxx no título.
- NUNCA enfraqueça, pule (skip/todo) ou apague um teste para passar — teste pulado não é prova e o audit acusa.
- Rode os testes localmente com `powershell -ExecutionPolicy Bypass -File scripts/verify-spec.ps1` até passarem.
- NÃO edite tasks.md, NÃO rode onp-spec verify/audit e NÃO toque em outras tarefas — o orquestrador cuida disso.
- Ao final de CADA tarefa: `git add` só no que você tocou e um commit próprio.' 'gpt-5.6-terra' medium >> "$LOG_DIR/seq.log" 2>&1; then
    # commit de segurança se o agente esqueceu (rastreabilidade > perfeição)
    if [ -n "$(git status --porcelain)" ]; then
      git add -A && git commit -q -m 'T-083 gestao-equipe-permissoes: Aplicar RequirePermission nos endpoints (auto-commit do plano)'
    fi
    marcar_concluidas T-083
    verde "✔ T-083 concluída"
    return 0
  fi
  vermelho "✘ T-083 falhou (log: $LOG_DIR/seq.log)"
  amarelo "  reexecute só ela: bash .spec/features/gestao-equipe-permissoes/executar-tarefas.sh --seq T-083"
  FALHAS="$FALHAS T-083"
  return 1
}

# ── sequencial T-084 (ordem do tasks.md) ──
executar_seq_T_084() {
  info 'sequencial T-084 — Endpoints de gestão de equipe listar/status'
  if rodar_tarefa seq 'T-084' 'Você executa UMA tarefa da feature "gestao-equipe-permissoes" (fluxo onp-spec, spec-anchored).
Leia primeiro: .spec/features/gestao-equipe-permissoes/spec.md, .spec/features/gestao-equipe-permissoes/tasks.md e .spec/constituicao.md.

Sua tarefa (somente ela):
T-084 — "Endpoints de gestão de equipe listar/status"
  critérios/refs: AC-072 (Isolamento multi-tenant), AC-073 (Impedir a desativação do último administrador)
  arquivos permitidos (e seus testes): src/Agendamento.Api/Features/Team/TeamMemberEndpoints.cs, src/Agendamento.Api/Program.cs
  mensagem de commit: "T-084 gestao-equipe-permissoes: Endpoints de gestão de equipe listar/status"

Regras inegociáveis:
- Todo critério de aceite referenciado vira teste com @spec:AC-xxx no título.
- NUNCA enfraqueça, pule (skip/todo) ou apague um teste para passar — teste pulado não é prova e o audit acusa.
- Rode os testes localmente com `powershell -ExecutionPolicy Bypass -File scripts/verify-spec.ps1` até passarem.
- NÃO edite tasks.md, NÃO rode onp-spec verify/audit e NÃO toque em outras tarefas — o orquestrador cuida disso.
- Ao final de CADA tarefa: `git add` só no que você tocou e um commit próprio.' 'gpt-5.6-terra' medium >> "$LOG_DIR/seq.log" 2>&1; then
    # commit de segurança se o agente esqueceu (rastreabilidade > perfeição)
    if [ -n "$(git status --porcelain)" ]; then
      git add -A && git commit -q -m 'T-084 gestao-equipe-permissoes: Endpoints de gestão de equipe listar/status (auto-commit do plano)'
    fi
    marcar_concluidas T-084
    verde "✔ T-084 concluída"
    return 0
  fi
  vermelho "✘ T-084 falhou (log: $LOG_DIR/seq.log)"
  amarelo "  reexecute só ela: bash .spec/features/gestao-equipe-permissoes/executar-tarefas.sh --seq T-084"
  FALHAS="$FALHAS T-084"
  return 1
}

# ── sequencial T-085 (ordem do tasks.md) ──
executar_seq_T_085() {
  info 'sequencial T-085 — Envio de convite e aceite'
  if rodar_tarefa seq 'T-085' 'Você executa UMA tarefa da feature "gestao-equipe-permissoes" (fluxo onp-spec, spec-anchored).
Leia primeiro: .spec/features/gestao-equipe-permissoes/spec.md, .spec/features/gestao-equipe-permissoes/tasks.md e .spec/constituicao.md.

Sua tarefa (somente ela):
T-085 — "Envio de convite e aceite"
  critérios/refs: AC-074 (Geração de convite único e expirável), AC-075 (Aceite de convite e definição de senha)
  arquivos permitidos (e seus testes): src/Agendamento.Api/Features/Team/TeamMemberEndpoints.cs, src/Agendamento.Api/Domain/Tenants/TeamInvitation.cs, src/Agendamento.Api/Infrastructure/Persistence/Configurations/TeamInvitationConfiguration.cs, src/Agendamento.Api/Infrastructure/Tenancy/HttpTenantContext.cs, src/Agendamento.Api/Infrastructure/Notifications/MockCommunicationGateway.cs
  mensagem de commit: "T-085 gestao-equipe-permissoes: Envio de convite e aceite"

Regras inegociáveis:
- Todo critério de aceite referenciado vira teste com @spec:AC-xxx no título.
- NUNCA enfraqueça, pule (skip/todo) ou apague um teste para passar — teste pulado não é prova e o audit acusa.
- Rode os testes localmente com `powershell -ExecutionPolicy Bypass -File scripts/verify-spec.ps1` até passarem.
- NÃO edite tasks.md, NÃO rode onp-spec verify/audit e NÃO toque em outras tarefas — o orquestrador cuida disso.
- Ao final de CADA tarefa: `git add` só no que você tocou e um commit próprio.' 'gpt-5.6-terra' medium >> "$LOG_DIR/seq.log" 2>&1; then
    # commit de segurança se o agente esqueceu (rastreabilidade > perfeição)
    if [ -n "$(git status --porcelain)" ]; then
      git add -A && git commit -q -m 'T-085 gestao-equipe-permissoes: Envio de convite e aceite (auto-commit do plano)'
    fi
    marcar_concluidas T-085
    verde "✔ T-085 concluída"
    return 0
  fi
  vermelho "✘ T-085 falhou (log: $LOG_DIR/seq.log)"
  amarelo "  reexecute só ela: bash .spec/features/gestao-equipe-permissoes/executar-tarefas.sh --seq T-085"
  FALHAS="$FALHAS T-085"
  return 1
}

# ── sequencial T-086 (ordem do tasks.md) ──
executar_seq_T_086() {
  info 'sequencial T-086 — Serialização de Enums e CORS'
  if rodar_tarefa seq 'T-086' 'Você executa UMA tarefa da feature "gestao-equipe-permissoes" (fluxo onp-spec, spec-anchored).
Leia primeiro: .spec/features/gestao-equipe-permissoes/spec.md, .spec/features/gestao-equipe-permissoes/tasks.md e .spec/constituicao.md.

Sua tarefa (somente ela):
T-086 — "Serialização de Enums e CORS"
  critérios/refs: AC-070 (Perfil completo do usuário)
  arquivos permitidos (e seus testes): src/Agendamento.Api/Program.cs, src/Agendamento.Api/Infrastructure/DependencyInjection.cs
  mensagem de commit: "T-086 gestao-equipe-permissoes: Serialização de Enums e CORS"

Regras inegociáveis:
- Todo critério de aceite referenciado vira teste com @spec:AC-xxx no título.
- NUNCA enfraqueça, pule (skip/todo) ou apague um teste para passar — teste pulado não é prova e o audit acusa.
- Rode os testes localmente com `powershell -ExecutionPolicy Bypass -File scripts/verify-spec.ps1` até passarem.
- NÃO edite tasks.md, NÃO rode onp-spec verify/audit e NÃO toque em outras tarefas — o orquestrador cuida disso.
- Ao final de CADA tarefa: `git add` só no que você tocou e um commit próprio.' 'gpt-5.6-terra' medium >> "$LOG_DIR/seq.log" 2>&1; then
    # commit de segurança se o agente esqueceu (rastreabilidade > perfeição)
    if [ -n "$(git status --porcelain)" ]; then
      git add -A && git commit -q -m 'T-086 gestao-equipe-permissoes: Serialização de Enums e CORS (auto-commit do plano)'
    fi
    marcar_concluidas T-086
    verde "✔ T-086 concluída"
    return 0
  fi
  vermelho "✘ T-086 falhou (log: $LOG_DIR/seq.log)"
  amarelo "  reexecute só ela: bash .spec/features/gestao-equipe-permissoes/executar-tarefas.sh --seq T-086"
  FALHAS="$FALHAS T-086"
  return 1
}

# ── sequencial T-087 (ordem do tasks.md) ──
executar_seq_T_087() {
  info 'sequencial T-087 — Configuração e validação de testes OpenAPI'
  if rodar_tarefa seq 'T-087' 'Você executa UMA tarefa da feature "gestao-equipe-permissoes" (fluxo onp-spec, spec-anchored).
Leia primeiro: .spec/features/gestao-equipe-permissoes/spec.md, .spec/features/gestao-equipe-permissoes/tasks.md e .spec/constituicao.md.

Sua tarefa (somente ela):
T-087 — "Configuração e validação de testes OpenAPI"
  critérios/refs: AC-070 (Perfil completo do usuário)
  arquivos permitidos (e seus testes): tests/Agendamento.IntegrationTests/Features/TeamMemberTests.cs, tests/Agendamento.IntegrationTests/Features/TeamFinancialHttpTests.cs, tests/Agendamento.IntegrationTests/Infrastructure/AgendamentoApiFactory.cs
  mensagem de commit: "T-087 gestao-equipe-permissoes: Configuração e validação de testes OpenAPI"

Regras inegociáveis:
- Todo critério de aceite referenciado vira teste com @spec:AC-xxx no título.
- NUNCA enfraqueça, pule (skip/todo) ou apague um teste para passar — teste pulado não é prova e o audit acusa.
- Rode os testes localmente com `powershell -ExecutionPolicy Bypass -File scripts/verify-spec.ps1` até passarem.
- NÃO edite tasks.md, NÃO rode onp-spec verify/audit e NÃO toque em outras tarefas — o orquestrador cuida disso.
- Ao final de CADA tarefa: `git add` só no que você tocou e um commit próprio.' 'gpt-5.6-terra' medium >> "$LOG_DIR/seq.log" 2>&1; then
    # commit de segurança se o agente esqueceu (rastreabilidade > perfeição)
    if [ -n "$(git status --porcelain)" ]; then
      git add -A && git commit -q -m 'T-087 gestao-equipe-permissoes: Configuração e validação de testes OpenAPI (auto-commit do plano)'
    fi
    marcar_concluidas T-087
    verde "✔ T-087 concluída"
    return 0
  fi
  vermelho "✘ T-087 falhou (log: $LOG_DIR/seq.log)"
  amarelo "  reexecute só ela: bash .spec/features/gestao-equipe-permissoes/executar-tarefas.sh --seq T-087"
  FALHAS="$FALHAS T-087"
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
      amarelo "  para o veredito: bash .spec/features/gestao-equipe-permissoes/executar-tarefas.sh --gate"
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
  executar_seq_T_080 || true
  executar_seq_T_081 || true
  executar_seq_T_082 || true
  executar_seq_T_083 || true
  executar_seq_T_084 || true
  executar_seq_T_085 || true
  executar_seq_T_086 || true
  executar_seq_T_087 || true
  encerrar tudo
}

listar() {
  echo "execução: $RUN_ID (feature $FEATURE, branch $BASE_BRANCH)"
  echo "  seq       T-080 (sequencial)"
  echo "  seq       T-081 (sequencial)"
  echo "  seq       T-082 (sequencial)"
  echo "  seq       T-083 (sequencial)"
  echo "  seq       T-084 (sequencial)"
  echo "  seq       T-085 (sequencial)"
  echo "  seq       T-086 (sequencial)"
  echo "  seq       T-087 (sequencial)"
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
      T-080) evento --tipo inicio --escopo "seq:T-080"; iniciar_resumos; executar_seq_T_080 || true; encerrar "seq:T-080" ;;
      T-081) evento --tipo inicio --escopo "seq:T-081"; iniciar_resumos; executar_seq_T_081 || true; encerrar "seq:T-081" ;;
      T-082) evento --tipo inicio --escopo "seq:T-082"; iniciar_resumos; executar_seq_T_082 || true; encerrar "seq:T-082" ;;
      T-083) evento --tipo inicio --escopo "seq:T-083"; iniciar_resumos; executar_seq_T_083 || true; encerrar "seq:T-083" ;;
      T-084) evento --tipo inicio --escopo "seq:T-084"; iniciar_resumos; executar_seq_T_084 || true; encerrar "seq:T-084" ;;
      T-085) evento --tipo inicio --escopo "seq:T-085"; iniciar_resumos; executar_seq_T_085 || true; encerrar "seq:T-085" ;;
      T-086) evento --tipo inicio --escopo "seq:T-086"; iniciar_resumos; executar_seq_T_086 || true; encerrar "seq:T-086" ;;
      T-087) evento --tipo inicio --escopo "seq:T-087"; iniciar_resumos; executar_seq_T_087 || true; encerrar "seq:T-087" ;;
      *) falhar "tarefa sequencial desconhecida: '$ALVO' — veja as disponíveis com --listar" ;;
    esac ;;
esac
