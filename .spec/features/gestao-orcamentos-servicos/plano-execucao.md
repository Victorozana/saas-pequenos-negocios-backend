# Plano de execução — gestao-orcamentos-servicos

> gerado por `onp-spec plano` em 2026-10-14 12:11 — NÃO edite à mão;
> mudou tasks.md ou a config? Regenere: `onp-spec plano gestao-orcamentos-servicos --sequencial`

## Resumo — o que vai acontecer

- **modo SEQUENCIAL (escolha do usuário)**: 7 tarefa(s) pendente(s), UMA APÓS A OUTRA, na árvore principal (6 já concluída(s): T-088, T-089, T-090, T-091, T-031, T-103)
- sem worktrees e sem paralelismo — cada tarefa roda numa janela de contexto limpa, na ordem do tasks.md
- tudo acontece na branch de trabalho `spec/gestao-orcamentos-servicos`; levar para a main é decisão sua

## Ordem de execução (uma tarefa após a outra)

| tarefa | título | modelo | esforço |
|---|---|---|---|
| T-092 | Testes de Integração para Orçamentos | `gpt-5.6-terra` | medium |
| T-032 | Mapear Persistência EF Core e Migrations | `gpt-5.6-terra` | medium |
| T-033 | Orquestrar Casos de Uso do Catálogo de Serviços/Produtos | `gpt-5.6-terra` | medium |
| T-034 | Orquestrar Casos de Uso da Gestão de Orçamentos | `gpt-5.6-terra` | medium |
| T-035 | Implementar Gerador de Exportação de Orçamento em PDF | `gpt-5.6-terra` | medium |
| T-036 | Expor Endpoints Minimal API de Serviços e Orçamentos | `gpt-5.6-terra` | medium |
| T-037 | Testes de Integração, Isolamento Multi-Tenant e Exportação PDF | `gpt-5.6-terra` | medium |

## Gestão de branches e commits

1. branch de trabalho `spec/gestao-orcamentos-servicos` criada do ponto atual (se ainda não existir)
2. as tarefas rodam nela mesma, na ordem — **1 tarefa = 1 commit** (`T-xxx feature: título`), marcada `[concluida]` só com trabalho feito
3. gate final na branch de trabalho: `onp-spec verify gestao-orcamentos-servicos` + `onp-spec audit --ci` — **exit 0 ou não está pronto**

## Como executar

### ▶ Execução — Codex headless (codex exec)

```bash
bash .spec/features/gestao-orcamentos-servicos/executar-tarefas.sh
```

Cada tarefa roda `codex exec` com **janela de contexto limpa**, na árvore principal,
uma após a outra, com `--model` e `model_reasoning_effort` já definidos por tarefa e sandbox `workspace-write`.
Os prompts exatos estão embutidos no script.
Logs: `../onp-worktrees/saas-pequenos-negocios-backend-gestao-orcamentos-servicos-logs/`.

**Confirmação de custos — antes de executar**: os modelos e esforços por
tarefa estão nas tabelas acima; o agente CONFIRMA com o usuário se estão
dentro da licença/cota dele (modelo forte + esforço alto torra tokens).
Para gastar menos: `onp-spec plano gestao-orcamentos-servicos --modelo gpt-5.6-luna --esforco baixo`
(tudo) ou por tarefa `onp-spec tarefa gestao-orcamentos-servicos T-xxx --modelo <m> --esforco <nível>` — e regenere o plano.

### 📣 Acompanhamento — tabela + resumo no chat (a cada 1 min)

O script roda em **background**: o agente AVISA o usuário antes de iniciar e,
enquanto roda, posta no chat a cada ~1 minuto a **tabela de andamento** (qual
tarefa está rodando, qual não está, o que concluiu/falhou) junto com o
**resumo geral de andamento** (escrito por IA; sem IA, o motor resume). Ao
final, o usuário recebe o resumo completo da execução. A qualquer momento:

```bash
onp-spec resumo gestao-orcamentos-servicos --tabela   # a tabela de andamento
onp-spec resumo gestao-orcamentos-servicos            # o resumo em texto
```

