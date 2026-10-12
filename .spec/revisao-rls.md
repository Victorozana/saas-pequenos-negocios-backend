# Revisão PostgreSQL/RLS — 12/10/2026

## Gate após a retomada RLS

- Auditoria global: exit 1, **6 erros e 0 avisos**; 89/94 critérios com prova granular.
- 15 features revalidadas depois da correção da fixture de concorrência. Cada execução final lê 175 resultados: 169 aprovados, zero falhas e seis skips antigos.
- Isolamento: 8/8 critérios PASS; AC-033/AC-034/AC-035 e P-004 possuem prova PostgreSQL real. T-019/T-020/T-021 concluídas com essas provas; isso não fecha a auditoria global.
- O teste financeiro agora exige duas consultas FOR UPDATE bloqueadas simultaneamente no PostgreSQL antes de liberar o orçamento. Pool de uma conexão apenas na prova específica de reutilização.
- Uma execução intermediária de equipe retornou exit 1. A repetição diagnóstica executou as três suítes e retornou exit 0; a causa intermediária não foi reproduzida. Não foi descartado teste nem suprimido erro. Log da repetição em .local-verification/rls-refresh-concurrency/equipe-runner.raw.log; prova final em .spec/verification/gestao-equipe-permissoes.json.
- Pendências do gate: AC-038 (minimização OpenAPI), AC-206 (edição pendente), US-052 (critério para fatura vencida), AC-097 (serviço centralizado), AC-098 (vínculo com cliente) e AC-207 (preferência de lembretes).
- Relatório final desta retomada: .spec/audit-rls.json. Relatório intermediário: .spec/audit-rls-intermediaria.json. Os relatórios anteriores e o histórico de sinais foram preservados.
- Sem aplicação em agendamento_local, sem alteração de cadastros ou segredos locais, sem commit/push. Frontend limpo. Antes de iniciar a API com esta versão, provisionar a migration e as duas credenciais restritas conforme docs/POSTGRESQL_RLS.md.
