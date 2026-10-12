# Contexto para a próxima conversa — 12/10/2026

## Continuidade da auditoria global — revisão adicional

Ler também `.spec/revisao-auditoria-global.md` e o relatório final `audit-global-final.json` quando disponível. A baseline desta continuação reproduziu 264 erros e 40 avisos; a revisão intermediária chegou a 143 erros e 33 avisos. Nenhuma dessas auditorias foi aprovada.

Foi corrigida uma falha reproduzida por testes: trocar o TenantId de uma entidade carregada de outra empresa permitia alterar/excluir o registro. O interceptor agora valida o TenantId original. Foram adicionadas provas de guarda de gravação, tenant externo em header/query/body e migrations PostgreSQL em duas bases temporárias isoladas. As asserções e os seis skips antigos foram preservados.

Requisitos e tarefas perdidos foram recuperados do histórico de serviços/orçamentos, financeiro e notificações. Por isso os números de critérios aumentam; não comparar apenas a porcentagem antiga de cada feature. As tarefas históricas recuperadas permanecem pendentes até revisão de abrangência das provas.

RLS continua sem certificação: AddRowLevelSecurity não aparece na lista de migrations descoberta pelo EF Core compilado. UnitOfWork configura contexto apenas em sua entrada de transação explícita e usa nome de configuração diferente do design. T-019/T-020/T-021 foram reabertas. A falsa atribuição de P-004 a um teste EF InMemory foi retirada, preservando o teste e suas asserções.

Novas decisões explícitas do usuário: orçamento enviado pode ser editado enquanto pendente, bloqueando edição após aprovação/rejeição/conversão (AC-206, T-103 pendente); conversão cria OS aguardando início manual; prestador pode desligar lembretes por cliente (AC-207, T-104 pendente). Essas confirmações não equivalem a implementação.

Planos das features com tarefas pendentes foram regenerados em modo sequencial. Os executores gerados não foram iniciados; os modelos históricos sugeridos pelo motor não foram adotados. A execução desta conversa permanece manual, com o modelo/esforço atuais.

Esta revisão não alterou o frontend, não aplicou migrations ao banco de trabalho e não realizou deploy. Os commits indicados pelo usuário na retomada são d79b630 (backend) e 37cd243 (frontend). Alterações locais preexistentes de obj foram preservadas. O gate global precisa continuar reprovado enquanto houver requisitos sem prova.

## Projeto e repositórios

SaaS de pequenos negócios, com empresas isoladas por tenant.

- Workspace: `C:\Users\Victor Ozana\Documents\agendamento`.
- Backend: `saas-pequenos-negocios-backend`, .NET 10, EF Core/PostgreSQL, branch `main`, remoto https://github.com/Victorozana/saas-pequenos-negocios-backend.
- Frontend: `saas-pequenos-negocios-frontend`, Next.js 16/React 19, branch `master`, remoto https://github.com/Victorozana/saas-pequenos-negocios-frontend.
- Os caminhos históricos em alguns AGENTS.md apontam para outra máquina. Trabalhar nos diretórios deste workspace.
- Ler AGENTS.md e a skill `.agents/skills/onp-spec-driven/SKILL.md` do backend. Conferir git status e commits antes de editar; preservar mudanças locais, especialmente arquivos obj já rastreados.

## Decisões do usuário

- Execução manual sequencial, mantendo o modelo e esforço atuais; não iniciar agentes ou scripts que escolham outros modelos automaticamente.
- Gerar recebíveis ao aprovar o orçamento, com entrada e saldo.
- Pagamentos manuais nesta etapa. Não integrar gateway agora.
- Esta revisão foi de backend e contrato; as telas de financeiro/equipe ainda serão desenvolvidas.
- O usuário autorizou explicitamente commits e push dos dois repositórios nesta conversa. Isso não certifica a auditoria global como aprovada.

## Ambiente local

PostgreSQL 18 está instalado e usa localhost:5432. Banco do projeto: `agendamento_local`. API Development em http://localhost:5050. A conexão é configurada em segredos locais do .NET, com UserSecretsId `agendamento-local-development`; nunca imprimir senha, token ou connection string.

`scripts/configurar-banco-local.ps1` pede a senha com entrada oculta e configura conexão/migrations. A migration `20261012002349_AddActiveTeamMembership` foi aplicada localmente, mantendo membros existentes ativos. Reiniciar a API pode invalidar sessões por causa da chave de assinatura de desenvolvimento; entrar novamente se necessário.

## O que foi corrigido

1. Conversão orçamento → OS não salvava no banco. Agora persiste a OS e o status convertido; uma repetição sequencial retorna a OS existente. Agendamentos conseguem usar o ID salvo. A atualização de status da OS também passa a persistir.
2. Frontend de orçamento consulta o catálogo ativo do tenant, permite selecionar serviço e preenche nome, unidade e preço; envia serviceItemId. Mantém descrição manual.
3. Alerta de UUID substituído por confirmação e botão “Agendar esta OS”, que abre formulário com vínculo preenchido.
4. Financeiro: recebíveis de entrada/saldo na aprovação, proteção contra duplicação em aprovações simultâneas, pagamentos manuais persistidos, saldo/extrato corretos, DTOs explícitos e permissões financial.read/financial.write.
5. Equipe: ativar/desativar vínculo sem bloquear outras empresas, proteger último Admin/owner_admin, revogar JWT anterior quando vínculo é desativado, aceitar convite sem sessão usando tenant do convite validado, rejeitar replay/expiração e preservar senha de usuário existente.
6. Tokens de convite não aparecem na resposta HTTP e o gateway mock não imprime conteúdo/destinatário. O envio de e-mail é simulado; configurar provedor antes de oferecer convites reais ao usuário.
7. Permissões aplicadas também a serviços, vendas, agenda e relatórios; autenticação verifica usuário e vínculo ativos.
8. OpenAPI/snapshot atualizados e schema tipado do frontend regenerado. IDs de critérios de assinaturas foram separados dos de equipe para evitar provas atribuídas à feature errada.

## Provas e pendências

Última verificação: 153 testes aprovados, zero reprovados, seis testes antigos pulados, 159 resultados lidos. Testes pulados não contam como prova. Financeiro: 3/3 critérios PASS; equipe: 6/6 PASS. As 12 tasks desses módulos estão concluídas, mas as features continuam sem fechamento global.

Teste PostgreSQL usa base temporária exclusiva criada/removida pelo próprio teste. Verifica pagamentos, convite público e aprovação concorrente sem duplicação. Não altera os cadastros do banco de trabalho.

`scripts/verify-spec.ps1` roda as três suítes e converte TRX para JSON granular. Executar:

```powershell
node .agents/skills/onp-spec-driven/scripts/onp-spec.mjs verify gestao-financeira-contas
node .agents/skills/onp-spec-driven/scripts/onp-spec.mjs verify gestao-equipe-permissoes
node .agents/skills/onp-spec-driven/scripts/onp-spec.mjs audit --ci
```

A auditoria global continua com exit 1: 264 erros e 40 avisos; não há achados específicos de financeiro/equipe. São problemas de outras specs e rastreabilidade global: formatos não reconhecidos, referências quebradas, IDs duplicados, arquivos mapeados ausentes, requisitos sem testes, provas fracas/antigas e código sem task. Não suprimir erros, enfraquecer testes ou inventar provas.

Detalhes: `.spec/revisao-financeiro-equipe.md`, `.spec/audit-financeiro-equipe.json` e provas em `.spec/verification/gestao-*.json`. O teste desta revisão comprova persistência local e isolamento HTTP; não certifica RLS de produção.

## Próximos passos recomendados

1. Priorizar e corrigir a auditoria global, começando pela integridade de specs/tasks e critérios duplicados. Revalidar o que for corrigido e implementar cobertura onde falta, sem marcar apenas por aparência.
2. Revisar isolamento-multitenancy/RLS e tarefas que apontam arquivos ausentes. Diferenciar erro documental de requisito realmente não implementado.
3. Sincronizar as tasks de orçamento/serviços que continuam pendentes com o código e testes existentes, respeitando o gate da skill.
4. Quando a base estiver auditada, especificar e implementar frontend financeiro: contas a receber/pagar, entrada/saldo, pagamentos parciais/totais e extrato, com erros e permissões adequados.
5. Implementar frontend de equipe: membros, papéis/permissões, ativação/desativação, convites e aceite. Configurar entrega real de e-mail antes de liberar convites.
6. Deixar gateway para depois. Distinguir cobrança da assinatura do SaaS de cobrança dos clientes de cada tenant; definir provedor/contas, tarifas, webhooks autenticados e idempotência antes de integrar.

Nenhum deploy foi realizado. Commits/push deste ponto de continuidade foram pedidos pelo usuário; obter os hashes pelo git log ou pela mensagem final desta conversa.

### Gate final executado

- onp-spec audit --ci: exit 1; 10 erros e 5 avisos.
- 86/94 critérios com prova granular; 15 features revalidadas.
- Suítes: 163 aprovados, zero falhas e seis skips antigos (169 resultados).
- Relatório completo: audit-global-final.json. Frontend limpo, commit 37cd243; alterações desta revisão permanecem locais no backend.
- A terceira auditoria manteve lacunas de RLS e requisitos sem teste. A skill determina parar e apresentar achados quando o mesmo problema persiste por três auditorias; nenhuma entrega global foi encerrada.

## Retomada RLS — 12/10/2026

Implementação manual, sem agentes/execução headless ou troca de modelo/esforço. Migration nova `20261012041401_EnforceTenantRowLevelSecurity`, descoberta pelo EF e com snapshot correspondente; a antiga AddRowLevelSecurity não foi ativada fora de ordem.

As 16 tabelas tenant-owned, incluindo itens de orçamento/OS e pagamentos, possuem tenant_id obrigatório, FK para tenants, índice iniciado pelo tenant e ENABLE/FORCE RLS. Policies permissivas e uma guarda restritiva usam app.tenant_id local com USING/WITH CHECK. Uma policy permissiva ampla adicional não remove a barreira. O backfill preserva registros antigos; FKs compostas rejeitam vínculos entre empresas.

TenantTransactionInterceptor cobre comandos EF síncronos/assíncronos, consultas, gravações, SQL e transações explícitas/implícitas. TenantConnectionInterceptor seleciona credenciais separadas e rejeita superuser/BYPASSRLS, proprietário e membership de sistema no login normal. Cadastro, autenticação, validação de sessão, aceite de convite e outbox usam escopo administrativo explícito com papel restrito e grants/policies específicos; webhook billing anônimo não recebe privilégio administrativo.

T-019/T-020/T-021 possuem implementação e provas. `verify isolamento-multitenancy`: 8/8 critérios PASS e P-004 com PostgreSQL real; 175 resultados, 169 aprovados, zero falhas e os seis skips antigos preservados. `has-pending-model-changes`: nenhuma diferença no modelo. As credenciais temporárias e bases exclusivas dos testes são removidas; segredos locais e cadastros de agendamento_local não foram alterados. Frontend permanece limpo em 37cd243; backend permanece no mesmo HEAD com alterações locais.

**Operação local ainda não provisionada:** a migration não foi aplicada em agendamento_local. Para iniciar a API com esta versão é preciso migration e logins distintos para PostgreSql e PostgreSqlAdministrative; a credencial antiga proprietária/superuser é recusada. PostgreSqlMigration mantém a credencial de provisionamento separada, usada também pelos testes locais. Ver docs/POSTGRESQL_RLS.md. Não confundir prova temporária com rollout local.

A auditoria global desta retomada fica em .spec/audit-rls.json; o relatório audit-global-final.json anterior foi preservado. As pendências seguintes continuam OpenAPI/minimização, notificações (serviço centralizado/vínculo de cliente/fatura vencida), edição de orçamento pendente e desativação de lembretes por cliente. Financeiro/equipe frontend e e-mail real aguardam essa continuidade. Nenhum fechamento global deve ser declarado enquanto o gate não sair 0.

## Gate após a retomada RLS

- Auditoria global: exit 1, **6 erros e 0 avisos**; 89/94 critérios com prova granular.
- 15 features revalidadas depois da correção da fixture de concorrência. Cada execução final lê 175 resultados: 169 aprovados, zero falhas e seis skips antigos.
- Isolamento: 8/8 critérios PASS; AC-033/AC-034/AC-035 e P-004 possuem prova PostgreSQL real. T-019/T-020/T-021 concluídas com essas provas; isso não fecha a auditoria global.
- O teste financeiro agora exige duas consultas FOR UPDATE bloqueadas simultaneamente no PostgreSQL antes de liberar o orçamento. Pool de uma conexão apenas na prova específica de reutilização.
- Uma execução intermediária de equipe retornou exit 1. A repetição diagnóstica executou as três suítes e retornou exit 0; a causa intermediária não foi reproduzida. Não foi descartado teste nem suprimido erro. Log da repetição em .local-verification/rls-refresh-concurrency/equipe-runner.raw.log; prova final em .spec/verification/gestao-equipe-permissoes.json.
- Pendências do gate: AC-038 (minimização OpenAPI), AC-206 (edição pendente), US-052 (critério para fatura vencida), AC-097 (serviço centralizado), AC-098 (vínculo com cliente) e AC-207 (preferência de lembretes).
- Relatório final desta retomada: .spec/audit-rls.json. Relatório intermediário: .spec/audit-rls-intermediaria.json. Os relatórios anteriores e o histórico de sinais foram preservados.
- Sem aplicação em agendamento_local, sem alteração de cadastros ou segredos locais, sem commit/push. Frontend limpo. Antes de iniciar a API com esta versão, provisionar a migration e as duas credenciais restritas conforme docs/POSTGRESQL_RLS.md.

## Retomada OpenAPI/minimização — 12/10/2026

Executada manualmente e sequencialmente, mantendo modelo/esforço, sem agentes ou execução headless. Backend permanece no HEAD fb5836d, frontend em 37cd243; alterações desta retomada permanecem locais, sem commit/push/deploy. Os dois repositórios estavam limpos ao iniciar; obj e histórico de sinais foram preservados.

AC-038/P-005 receberam provas estruturais e HTTP: percurso de respostas/headers e exemplos resolve referências, arrays, allOf/oneOf/anyOf e additionalProperties, protege ciclos e rejeita referências inválidas/exemplos externos. Casos negativos provam o detector. Entradas de senha, CPF e token continuam legítimas; token de sessão é saída somente do login. Não atribuir essa prova somente à antiga busca de palavras.

Removidos token bruto e X-Development-Verification-Token da resposta de cadastro em todos os ambientes, inclusive Development. Removida a ocultação por transformer; DTO/runtime agora coincidem. AC-100/ASM-013 foram explicitamente revisados conforme a nova instrução. O handler não devolve token e a falha de cadastro não expõe ex.Message. Onboarding HTTP continua comprovando geração de token, confirmação, login e perfil com token controlado apenas na fixture; não certifica entrega real de e-mail.

CPF integral também saía sob documentValue. Lista e detalhe de clientes agora devolvem máscara com apenas dois dígitos finais, com pattern correspondente no schema; CNPJ continua disponível. PreserveDocument permite editar outros campos sem reenviar CPF. O frontend mantém a máscara e usa essa flag, sem enviá-la como documento substituto.

Respostas JSON de perfil do tenant, clientes, IDs de criação/conversão, notificações, planos e assinatura atual foram tipadas; Bearer é declarado por operação respeitando AllowAnonymous. docs/openapi/v1.json e schema.d.ts do frontend regenerados. BFF do cadastro retorna só message; confirmação usa código informado separadamente. E-mail real permanece pendente.

T-025 e T-101 concluídas após verify exit 0. T-023 permanece pendente pelas divergências reais de erros ProblemDetails, respostas sem corpo e exports binários. Plano do contrato regenerado sequencialmente; executor não iniciado e suas sugestões históricas de modelo não foram adotadas.

As 15 features foram revalidadas com a versão final de código/testes: 198 resultados por execução, 192 aprovados, zero falhas, seis skips antigos preservados. Frontend: typecheck, ESLint dos arquivos editados e 38 testes relevantes passaram após a última mudança; execução integral de 207 testes ocorreu antes da correção de CPF. Não apresentar esse total antigo como suíte integral da versão final.

Falhas intermediárias e diagnóstico estão em .spec/revisao-openapi.md. O teste de build Release sem avisos falhou intermitentemente em duas execuções; agora registra saída completa mantendo todas as asserções. A causa do aviso ainda não foi isolada. Todas as últimas revalidações passaram. Uma fixture Development tentou acessar a conexão local e foi recusada; corrigida para InMemory, sem cadastro/migration no banco de trabalho.

Auditoria intermediária: .spec/audit-openapi-intermediaria.json, exit 1, 18 erros/zero avisos (cinco requisitos restantes e 13 provas obsoletas). As 13 provas foram renovadas. Veredito final: .spec/audit-openapi.json; não declarar aprovação global. Permanecem AC-206, US-052 sem critério, AC-097, AC-098 e AC-207. Financeiro/equipe frontend e e-mail real seguem após resolver requisitos e gate.

A migration RLS segue NÃO aplicada a agendamento_local. Não foram alterados cadastros nem segredos locais; ler POSTGRESQL_RLS.md antes de iniciar API. Quatro saídas build-openapi-diagnostic desta tarefa ficaram ignoradas pelo Git porque a limpeza foi bloqueada por política; não tocar em obj ou trabalho preexistente para removê-las.

## Versionamento e próxima prioridade — 12/10/2026

O usuário autorizou explicitamente commit e push das alterações desta retomada nos dois repositórios. Obter os hashes finais por git log -1 em cada repositório; os hashes fb5836d/37cd243 acima são a base anterior às alterações OpenAPI.

Auditoria final .spec/audit-openapi.json: exit 1, cinco erros, zero avisos, 90/94 critérios com prova granular. Próxima prioridade recomendada: implementar o serviço centralizado de notificações (AC-097) e o vínculo do histórico ao cliente (AC-098), com isolamento por tenant e preservação das provas de outbox/retries. Depois, implementar a preferência por cliente (AC-207), definir com o usuário o disparo executável de fatura vencida (US-052) e resolver edição de orçamento pendente (AC-206/T-103). T-023 permanece pendente pelas divergências reais documentadas. Não considerar a auditoria aprovada nem iniciar telas de financeiro/equipe ou e-mail real antes da base auditada.
