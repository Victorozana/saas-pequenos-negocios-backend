# Plano de execução — gestao-equipe-permissoes

> gerado por `onp-spec plano` em 2026-10-12 00:26 — NÃO edite à mão;
> mudou tasks.md ou a config? Regenere: `onp-spec plano gestao-equipe-permissoes --sequencial`

## Resumo — o que vai acontecer

- **modo SEQUENCIAL (escolha do usuário)**: 8 tarefa(s) pendente(s), UMA APÓS A OUTRA, na árvore principal
- sem worktrees e sem paralelismo — cada tarefa roda numa janela de contexto limpa, na ordem do tasks.md
- tudo acontece na branch de trabalho `spec/gestao-equipe-permissoes`; levar para a main é decisão sua

## Ordem de execução (uma tarefa após a outra)

| tarefa | título | modelo | esforço |
|---|---|---|---|
| T-080 | Atualizar entidade User com nome e DTO Profile | `gpt-5.6-terra` | medium |
| T-081 | Definir Permissions e autorização em TenantMembership | `gpt-5.6-terra` | medium |
| T-082 | Middleware/Handler de Permissões | `gpt-5.6-terra` | medium |
| T-083 | Aplicar RequirePermission nos endpoints | `gpt-5.6-terra` | medium |
| T-084 | Endpoints de gestão de equipe listar/status | `gpt-5.6-terra` | medium |
| T-085 | Envio de convite e aceite | `gpt-5.6-terra` | medium |
| T-086 | Serialização de Enums e CORS | `gpt-5.6-terra` | medium |
| T-087 | Configuração e validação de testes OpenAPI | `gpt-5.6-terra` | medium |

## Gestão de branches e commits

1. branch de trabalho `spec/gestao-equipe-permissoes` criada do ponto atual (se ainda não existir)
2. as tarefas rodam nela mesma, na ordem — **1 tarefa = 1 commit** (`T-xxx feature: título`), marcada `[concluida]` só com trabalho feito
3. gate final na branch de trabalho: `onp-spec verify gestao-equipe-permissoes` + `onp-spec audit --ci` — **exit 0 ou não está pronto**

## Como executar

### ▶ Execução — Codex headless (codex exec)

```bash
bash .spec/features/gestao-equipe-permissoes/executar-tarefas.sh
```

Cada tarefa roda `codex exec` com **janela de contexto limpa**, na árvore principal,
uma após a outra, com `--model` e `model_reasoning_effort` já definidos por tarefa e sandbox `workspace-write`.
Os prompts exatos estão embutidos no script.
Logs: `../onp-worktrees/saas-pequenos-negocios-backend-gestao-equipe-permissoes-logs/`.

**Confirmação de custos — antes de executar**: os modelos e esforços por
tarefa estão nas tabelas acima; o agente CONFIRMA com o usuário se estão
dentro da licença/cota dele (modelo forte + esforço alto torra tokens).
Para gastar menos: `onp-spec plano gestao-equipe-permissoes --modelo gpt-5.6-luna --esforco baixo`
(tudo) ou por tarefa `onp-spec tarefa gestao-equipe-permissoes T-xxx --modelo <m> --esforco <nível>` — e regenere o plano.

### 📣 Acompanhamento — tabela + resumo no chat (a cada 1 min)

O script roda em **background**: o agente AVISA o usuário antes de iniciar e,
enquanto roda, posta no chat a cada ~1 minuto a **tabela de andamento** (qual
tarefa está rodando, qual não está, o que concluiu/falhou) junto com o
**resumo geral de andamento** (escrito por IA; sem IA, o motor resume). Ao
final, o usuário recebe o resumo completo da execução. A qualquer momento:

```bash
onp-spec resumo gestao-equipe-permissoes --tabela   # a tabela de andamento
onp-spec resumo gestao-equipe-permissoes            # o resumo em texto
```
