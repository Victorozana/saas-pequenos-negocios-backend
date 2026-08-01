# Plano de execução — contrato-openapi-swagger

> gerado por `onp-spec plano` em 2026-08-01 18:48 — NÃO edite à mão;
> mudou tasks.md ou a config? Regenere: `onp-spec plano contrato-openapi-swagger`

## Resumo — o que vai acontecer

- **4 tarefa(s) pendente(s)**: 4 em 4 faixa(s) paralela(s) + 0 sequencial(is)
- **1 faixa = 1 worktree + 1 branch + 1 janela de contexto limpa** — faixas não compartilham nenhum arquivo entre si
- prefere outra seleção ou uma após a outra? Regenere com `onp-spec plano contrato-openapi-swagger --paralelizar T-xxx,T-yyy` ou `--sequencial`
- tudo acontece na branch de trabalho `spec/contrato-openapi-swagger`; levar para a main é decisão sua

## Faixas e ondas

### Onda 1 — faixa-1 ∥ faixa-2 ∥ faixa-3

#### faixa-1 — branch `spec/contrato-openapi-swagger-faixa-1` — worktree `../onp-worktrees/agendamento-contrato-openapi-swagger-faixa-1`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-022 | Registrar OpenAPI e Swagger UI | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Agendamento.Api.csproj`, `src/Agendamento.Api/Program.cs`, `src/Agendamento.Api/OpenApi/OpenApiExtensions.cs`, `tests/Agendamento.IntegrationTests/OpenApi/SwaggerUiTests.cs` |

#### faixa-2 — branch `spec/contrato-openapi-swagger-faixa-2` — worktree `../onp-worktrees/agendamento-contrato-openapi-swagger-faixa-2`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-023 | Documentar endpoints, segurança e erros | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Features/Health`, `src/Agendamento.Api/Features/Identity`, `src/Agendamento.Api/Features/Tenants`, `src/Agendamento.Api/OpenApi/ProblemDetailsOperationTransformer.cs`, `src/Agendamento.Api/OpenApi/BearerSecuritySchemeTransformer.cs`, `tests/Agendamento.ArchitectureTests/OpenApi/EndpointMetadataTests.cs` |

#### faixa-3 — branch `spec/contrato-openapi-swagger-faixa-3` — worktree `../onp-worktrees/agendamento-contrato-openapi-swagger-faixa-3`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-024 | Gerar contrato canônico versionado | `gpt-5.6-terra` | medium | `docs/openapi/v1.json`, `tools/export-openapi.ps1`, `tests/Agendamento.IntegrationTests/OpenApi/OpenApiSnapshotTests.cs` |

### Onda 2 — faixa-4

#### faixa-4 — branch `spec/contrato-openapi-swagger-faixa-4` — worktree `../onp-worktrees/agendamento-contrato-openapi-swagger-faixa-4`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-025 | Validar completude e minimização dos schemas | `gpt-5.6-terra` | medium | `tests/Agendamento.ArchitectureTests/OpenApi/OpenApiCompletenessTests.cs`, `tests/Agendamento.ArchitectureTests/OpenApi/OpenApiSensitiveDataTests.cs` |

## Gestão de branches e commits

1. branch de trabalho `spec/contrato-openapi-swagger` criada do ponto atual (se ainda não existir)
2. cada faixa nasce dela como branch própria e roda no seu worktree — **1 tarefa = 1 commit** (`T-xxx feature: título`)
3. terminou a onda → merge `--no-ff` de cada faixa de volta, na ordem; conflito interrompe a faixa e pede resolução humana
4. faixa mesclada → worktree removido, branch apagada, tarefa marcada `[concluida]` no tasks.md
5. gate final na branch de trabalho: `onp-spec verify contrato-openapi-swagger` + `onp-spec audit --ci` — **exit 0 ou não está pronto**

## Como executar

### ▶ Execução — Codex headless (codex exec)

```bash
bash .spec/features/contrato-openapi-swagger/executar-tarefas.sh
```

Cada faixa roda `codex exec` com **janela de contexto limpa**, no seu worktree, com
`--model` e `model_reasoning_effort` já definidos por tarefa e sandbox `workspace-write`. Os prompts exatos estão
embutidos no script — quer rodar uma faixa na mão, é só copiá-los de lá.
Logs: `../onp-worktrees/agendamento-contrato-openapi-swagger-logs/`.

**Confirmação de custos — antes de executar**: os modelos e esforços por
tarefa estão nas tabelas acima; o agente CONFIRMA com o usuário se estão
dentro da licença/cota dele (modelo forte + esforço alto torra tokens).
Para gastar menos: `onp-spec plano contrato-openapi-swagger --modelo gpt-5.6-luna --esforco baixo`
(tudo) ou por tarefa `onp-spec tarefa contrato-openapi-swagger T-xxx --modelo <m> --esforco <nível>` — e regenere o plano.

### 📣 Acompanhamento — tabela + resumo no chat (a cada 1 min)

O script roda em **background**: o agente AVISA o usuário antes de iniciar e,
enquanto roda, posta no chat a cada ~1 minuto a **tabela de andamento** (qual
tarefa está rodando, qual não está, o que concluiu/falhou) junto com o
**resumo geral de andamento** (escrito por IA; sem IA, o motor resume). Ao
final, o usuário recebe o resumo completo da execução. A qualquer momento:

```bash
onp-spec resumo contrato-openapi-swagger --tabela   # a tabela de andamento
onp-spec resumo contrato-openapi-swagger            # o resumo em texto
```

