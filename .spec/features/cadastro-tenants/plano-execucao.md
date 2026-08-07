# Plano de execução — cadastro-tenants

> gerado por `onp-spec plano` em 2026-08-06 23:18 — NÃO edite à mão;
> mudou tasks.md ou a config? Regenere: `onp-spec plano cadastro-tenants`

## Resumo — o que vai acontecer

- **7 tarefa(s) pendente(s)**: 7 em 7 faixa(s) paralela(s) + 0 sequencial(is)
- **1 faixa = 1 worktree + 1 branch + 1 janela de contexto limpa** — faixas não compartilham nenhum arquivo entre si
- prefere outra seleção ou uma após a outra? Regenere com `onp-spec plano cadastro-tenants --paralelizar T-xxx,T-yyy` ou `--sequencial`
- tudo acontece na branch de trabalho `spec/cadastro-tenants`; levar para a main é decisão sua

## Faixas e ondas

### Onda 1 — faixa-1 ∥ faixa-2 ∥ faixa-3

#### faixa-1 — branch `spec/cadastro-tenants-faixa-1` — worktree `../onp-worktrees/agendamento-cadastro-tenants-faixa-1`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-010 | Modelar tenant, endereço e perfil fiscal | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Domain/Tenants/Tenant.cs`, `src/Agendamento.Api/Domain/Tenants/TenantAddress.cs`, `src/Agendamento.Api/Domain/Tenants/TenantFiscalProfile.cs`, `src/Agendamento.Api/Domain/Tenants/TaxRegime.cs`, `src/Agendamento.Api/Domain/Tenants/BusinessCategory.cs`, `tests/Agendamento.UnitTests/Tenants/TenantTests.cs`, `tests/Agendamento.UnitTests/Tenants/TenantFiscalProfileTests.cs` |

#### faixa-2 — branch `spec/cadastro-tenants-faixa-2` — worktree `../onp-worktrees/agendamento-cadastro-tenants-faixa-2`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-011 | Implementar CNPJ legado e alfanumérico | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Domain/Tenants/Cnpj.cs`, `tests/Agendamento.UnitTests/Tenants/CnpjTests.cs` |

#### faixa-3 — branch `spec/cadastro-tenants-faixa-3` — worktree `../onp-worktrees/agendamento-cadastro-tenants-faixa-3`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-012 | Consultar elegibilidade cadastral | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Application/Tenants/CompanyRegistry/ICompanyRegistryGateway.cs`, `src/Agendamento.Api/Application/Tenants/CompanyRegistry/CompanyRegistryResult.cs`, `src/Agendamento.Api/Application/Tenants/CompanyRegistry/FoodCnaePolicy.cs`, `src/Agendamento.Api/Application/Tenants/LookupCompany/LookupCompanyQuery.cs`, `src/Agendamento.Api/Application/Tenants/LookupCompany/LookupCompanyHandler.cs`, `src/Agendamento.Api/Infrastructure/CompanyRegistry/CompanyRegistryGateway.cs`, `tests/Agendamento.UnitTests/Tenants/FoodCnaePolicyTests.cs`, `tests/Agendamento.IntegrationTests/Tenants/CompanyRegistryContractTests.cs` |

### Onda 2 — faixa-4 ∥ faixa-5 ∥ faixa-6

#### faixa-4 — branch `spec/cadastro-tenants-faixa-4` — worktree `../onp-worktrees/agendamento-cadastro-tenants-faixa-4`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-013 | Mapear persistência e migration do cadastro | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Infrastructure/Persistence/AgendamentoDbContext.cs`, `src/Agendamento.Api/Infrastructure/Persistence/Configurations/TenantConfiguration.cs`, `src/Agendamento.Api/Infrastructure/Persistence/Configurations/TenantFiscalProfileConfiguration.cs`, `src/Agendamento.Api/Infrastructure/Persistence/Configurations/TenantMembershipConfiguration.cs`, `src/Agendamento.Api/Infrastructure/Persistence/Migrations`, `tests/Agendamento.IntegrationTests/Tenants/TenantPersistenceTests.cs` |

#### faixa-5 — branch `spec/cadastro-tenants-faixa-5` — worktree `../onp-worktrees/agendamento-cadastro-tenants-faixa-5`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-014 | Orquestrar cadastro transacional e idempotente | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Application/Tenants/RegisterTenant/RegisterTenantCommand.cs`, `src/Agendamento.Api/Application/Tenants/RegisterTenant/RegisterTenantHandler.cs`, `src/Agendamento.Api/Application/Common/IUnitOfWork.cs`, `src/Agendamento.Api/Infrastructure/Persistence/IdempotencyRecord.cs`, `tests/Agendamento.UnitTests/Tenants/RegisterTenantHandlerTests.cs`, `tests/Agendamento.IntegrationTests/Tenants/RegisterTenantTransactionTests.cs` |

#### faixa-6 — branch `spec/cadastro-tenants-faixa-6` — worktree `../onp-worktrees/agendamento-cadastro-tenants-faixa-6`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-015 | Expor consulta e cadastro públicos | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Features/Tenants/CompanyRegistryEndpoints.cs`, `src/Agendamento.Api/Features/Tenants/TenantRegistrationEndpoints.cs`, `src/Agendamento.Api/Features/Tenants/RegisterTenantRequest.cs`, `src/Agendamento.Api/Features/Tenants/RegisterTenantResponse.cs`, `src/Agendamento.Api/Features/Common/ProblemDetailsExtensions.cs`, `tests/Agendamento.IntegrationTests/Tenants/TenantRegistrationEndpointTests.cs` |

### Onda 3 — faixa-7

#### faixa-7 — branch `spec/cadastro-tenants-faixa-7` — worktree `../onp-worktrees/agendamento-cadastro-tenants-faixa-7`

| tarefa | título | modelo | esforço | arquivos |
|---|---|---|---|---|
| T-016 | Expor perfis autenticados | `gpt-5.6-terra` | medium | `src/Agendamento.Api/Application/Tenants/GetCurrentTenant/GetCurrentTenantQuery.cs`, `src/Agendamento.Api/Application/Identity/GetCurrentUser/GetCurrentUserQuery.cs`, `src/Agendamento.Api/Features/Tenants/CurrentTenantEndpoints.cs`, `src/Agendamento.Api/Features/Identity/CurrentUserEndpoints.cs`, `tests/Agendamento.IntegrationTests/Tenants/CurrentTenantEndpointTests.cs`, `tests/Agendamento.IntegrationTests/Identity/CurrentUserEndpointTests.cs` |

## Gestão de branches e commits

1. branch de trabalho `spec/cadastro-tenants` criada do ponto atual (se ainda não existir)
2. cada faixa nasce dela como branch própria e roda no seu worktree — **1 tarefa = 1 commit** (`T-xxx feature: título`)
3. terminou a onda → merge `--no-ff` de cada faixa de volta, na ordem; conflito interrompe a faixa e pede resolução humana
4. faixa mesclada → worktree removido, branch apagada, tarefa marcada `[concluida]` no tasks.md
5. gate final na branch de trabalho: `onp-spec verify cadastro-tenants` + `onp-spec audit --ci` — **exit 0 ou não está pronto**

## Como executar

### ▶ Execução — Codex headless (codex exec)

```bash
bash .spec/features/cadastro-tenants/executar-tarefas.sh
```

Cada faixa roda `codex exec` com **janela de contexto limpa**, no seu worktree, com
`--model` e `model_reasoning_effort` já definidos por tarefa e sandbox `workspace-write`. Os prompts exatos estão
embutidos no script — quer rodar uma faixa na mão, é só copiá-los de lá.
Logs: `../onp-worktrees/agendamento-cadastro-tenants-logs/`.

**Confirmação de custos — antes de executar**: os modelos e esforços por
tarefa estão nas tabelas acima; o agente CONFIRMA com o usuário se estão
dentro da licença/cota dele (modelo forte + esforço alto torra tokens).
Para gastar menos: `onp-spec plano cadastro-tenants --modelo gpt-5.6-luna --esforco baixo`
(tudo) ou por tarefa `onp-spec tarefa cadastro-tenants T-xxx --modelo <m> --esforco <nível>` — e regenere o plano.

### 📣 Acompanhamento — tabela + resumo no chat (a cada 1 min)

O script roda em **background**: o agente AVISA o usuário antes de iniciar e,
enquanto roda, posta no chat a cada ~1 minuto a **tabela de andamento** (qual
tarefa está rodando, qual não está, o que concluiu/falhou) junto com o
**resumo geral de andamento** (escrito por IA; sem IA, o motor resume). Ao
final, o usuário recebe o resumo completo da execução. A qualquer momento:

```bash
onp-spec resumo cadastro-tenants --tabela   # a tabela de andamento
onp-spec resumo cadastro-tenants            # o resumo em texto
```

