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

## Retomada de notificações — 12/10/2026

Base backend main/96875bb e frontend master/ca1d485; execução manual sequencial, modelo/esforço
atuais, sem agentes/executores de modelos. Alterações desta retomada permanecem locais,
sem commit/push/deploy; obj e histórico de sinais preservados.

AC-097/AC-098 implementados e provados. INotificationService centraliza validação/staging e
processamento da outbox; ICommunicationGateway continua adaptador substituível. Agenda,
lembretes, orçamento, envio manual e convite usam o serviço. Queue não salva nem envia;
o caso de uso confirma no mesmo DbContext. Worker mantém lote 50, polling 30s, elegibilidade,
retries de 2/4 minutos e limite de três tentativas. Envio continua simulado.

NotificationMessage possui CustomerId e Audience. Mensagens de cliente exigem vínculo;
cliente externo/inexistente é rejeitado antes de staging e pela FK composta tenant/cliente.
Avulsa sem cliente é Standalone, convite é System: ambos ficam no histórico geral do tenant,
sem fingir interação de cliente. Legado é LegacyUnlinked, preservando todos os campos antigos,
sem inferir cliente por telefone/e-mail. Ver design/spec e .spec/revisao-notificacoes.md.

Migration nova 20261012154514_AddNotificationCustomerHistory: CHECK, FK composta com Restrict,
índice iniciado pelo tenant e backfill conservador. Provada em duas bases temporárias
exclusivas a partir de RLS, comparando todos os campos antigos e exercitando credenciais
restritas no HTTP/outbox/histórico. Nem RLS nem esta migration foram aplicadas a
agendamento_local. Não alterados cadastros ou segredos; ler POSTGRESQL_RLS.md antes da API.

Contrato: histórico aceita filtro customerId e retorna CustomerId/Audience; manual aceita
CustomerId opcional sem fallback se inválido. OpenAPI e schema frontend regenerados.
T-023 permanece pendente pelas divergências documentadas anteriormente.

T-097/T-098/T-100 concluídas após verify exit 0; plano sequencial regenerado, executor não
iniciado e modelos históricos sugeridos não adotados. As 15 features revalidadas com 207
resultados finais por execução: 201 PASS, zero falhas, seis skips antigos. Frontend:
typecheck e suíte integral de 209 testes/45 arquivos PASS. EF sem pending model changes;
git diff --check dos dois repositórios passou.

Build Release voltou a falhar intermitentemente durante uma renovação; agenda tinha critérios
PASS, mas runner exit 1 por AC-001. CLI do motor retornou 0 por considerar apenas critérios
da feature. Isso foi detectado e interrompido; repetição passou sem alterar testes/asserts.
A causa permanece sem diagnóstico definitivo. Registro falho e limitação do TRX substituído
estão documentados na revisão; retomada exige record.exitCode=0 e preserva TRX por feature.
Logs em .local-verification/notifications/.

Relatório final: .spec/audit-notificacoes.json. Não declarar auditoria aprovada. Próximas
pendências continuam AC-207/T-104 (preferência por cliente), US-052 (definir com o usuário
regra executável de fatura vencida) e AC-206/T-103 (edição de orçamento pendente).
Financeiro/equipe frontend e entrega real de e-mail seguem após resolver requisitos e gate.

Gate final desta etapa: exit 1, três erros, zero avisos, 92/94 critérios com prova granular.
Achados somente de AC-206, US-052 e AC-207. AC-097/AC-098 possuem provas. Relatório JSON
consultável em .spec/audit-notificacoes.json; saída bruta e logs em .local-verification/notifications/.
O HTML regenerado do plano teve apenas espaços finais normalizados para passar diff --check.

## Correção da inicialização local — 12/10/2026

Usuário pediu corrigir falha e enviou o comando dotnet run do perfil http: build passava, mas
DbSeeder caía porque PostgreSqlAdministrative não estava configurada. A operação local descrita
como pendente nas seções anteriores foi concluída nesta correção: backup custom preservado,
restauração exclusiva validada com integridade das 21 tabelas, aplicação das duas migrations
RLS/notificações a agendamento_local e dois logins restritos separados em segredos .NET.
Credencial anterior preservada como PostgreSqlMigration. Cadastros não foram alterados.

API iniciou com o comando original, health/OpenAPI 200 e consulta EF com login normal restrito
PASS. Worker atualizou mensagens pendentes com envio simulado, sem envio real. Cópia temporária
removida; backup em .local-verification/runtime-repair/agendamento_local-before-rls.dump.
Execução oculta de validação encerrada para liberar porta 5050. Frontend npm run build PASS.

scripts/configurar-banco-local.ps1 passa a gravar PostgreSqlMigration, preservando conexões de
runtime. Não remover as proteções do TenantConnectionInterceptor para solucionar configuração.
Avisos ValueComparer não causavam a queda; intermitência histórica do teste de build continua
separada. Alterações anteriores, obj/sinais e schema gerado preservados; sem commit/push/deploy.

## Preferência de cliente e edição de orçamento — 12/10/2026

Retomada manual em backend main/96875bb e frontend master/ca1d485. Agentes auxiliares
autorizados pelo usuário, sem alteração de modelo/esforço. Todas as mudanças anteriores,
obj, schema, sinais e relatórios preservados; nenhuma operação de commit/push/deploy.

AC-207/T-104 implementado: AutomaticRemindersEnabled persistido em Customer, default true
no cadastro e para registros existentes, update nullable preservando escolha omitida, lista
e detalhe devolvendo preferência. Checkbox no frontend. CPF mascarado/PreserveDocument e
substituição explícita de CPF preservados. QueueAutomaticReminderAsync valida tenant/cliente
e opt-out antes da fila nos dois produtores de agenda; manual/orçamento/System permanecem.
Histórico já enfileirado não é alterado. Sete novas provas HTTP/serviço/PostgreSQL passaram.

Migration 20261012174238_AddCustomerAutomaticReminderPreference validada apenas em base
temporária exclusiva: compara todos os campos anteriores, default/backfill, persistência
e RLS. NÃO aplicada a agendamento_local; a versão nova requer essa coluna antes de iniciar
contra o banco de trabalho. API preexistente PID 27004/porta 5050 foi preservada. O rollout
RLS/histórico documentado na seção anterior continua concluído, independente desta migration.

AC-206/T-103 implementado: PUT de conteúdo completo apenas Pending; valida antes de alterar,
substitui itens e recalcula total/entrada, protege tenant/cliente/catálogo e compartilha lock
com aprovação. Estados finais permanecem imutáveis. Seis provas HTTP/PostgreSQL, incluindo
espera real de lock na edição concorrente, passaram. Frontend oferece edição só Pending,
carrega detalhes e preserva código, data de emissão, pagamento e entrada.

US-052 definido pelo usuário: disparo automático diário para conta vencida com saldo aberto,
um aviso por conta por dia até quitar ou cancelar. Q-018/Q-019 respondidos, AC-208 criado,
T-105 PENDENTE. Definição não é implementação nem prova. Próxima prioridade é T-105, com
deduplicação durável por tenant/conta/dia, data controlável, opt-out e provas em bases exclusivas.
Não avançar para financeiro/equipe frontend ou e-mail real enquanto o gate continuar pendente.
T-023 e demais tarefas históricas não foram marcadas por aparência.

Duas falhas intermediárias preservadas: MSB3026 em recursos comuns de testhost no OutDir
compartilhado de SolutionBuildTests (projetos agora serializados mantendo todas as asserções),
e PID diferente em AC-034 causado por ClearAllPools de outro teste (cleanup agora só ClearPool
das bases exclusivas da própria fixture). Não atribuir automaticamente essa causa às falhas
históricas sem diagnóstico. Evidências em .local-verification/customer-reminders/failed-*.

As 15 features foram revalidadas após a versão final: motor=0/runner=0 em todas, 220 resultados
por execução (214 PASS, zero falhas e seis skips antigos). TRX/JSON separados por feature.
Frontend final: 221 testes em 47 arquivos, typecheck, build e ESLint dos arquivos alterados PASS.
OpenAPI/schema regenerados; EF sem pending model changes e diff --check dos dois repos PASS.
Planos regenerados sem executar headless/modelos históricos. Revisão detalhada em
.spec/revisao-preferencia-clientes.md; veredito final em .spec/audit-preferencia-clientes.json.

## Aviso automático diário de conta vencida — 12/10/2026

AC-208/T-105 implementado e provado. Worker dedicado inicia automaticamente e reconcilia
a cada minuto, sem horário operacional fixo. TimeProvider controla o dia UTC seguindo o
calendário financeiro existente. INotificationService valida conta/cliente/preferência,
saldo e vencimento e confirma somente a intenção na outbox. FOR UPDATE por conta e índice
único tenant/conta/dia impedem duplicação entre jobs concorrentes e reinícios. Retries mantêm
o mesmo registro; no próximo dia a conta ainda elegível recebe outra intenção.

Quatro provas PostgreSQL novas PASS: elegibilidade/partial/zero/hoje/futuro/opt-out,
repetição, reinício, dia seguinte, quitação/cancelamento, outra conta/empresa, worker real,
concorrência observada com dois waiters, índice único pelo nome, FKs externas rejeitadas,
upgrade preservando campos históricos e privilégios administrativos mínimos.
Evidência focada final em .local-verification/overdue-reminders/proofs-20261012-162507/.
Falhas intermediárias preservadas: fixture de pagamentos/status e privilégio administrativo
ausente nos recebíveis (corrigido na migration, sem enfraquecer testes).

Migration adicional 20261012191900_AddDailyReceivableReminderDeduplication validada SOMENTE
em bases temporárias exclusivas. Permite leitura e FOR UPDATE pelo papel de sistema, mas
policy WITH CHECK false rejeita gravações financeiras. Login normal mantém RLS/guarda.
Esta migration e AddCustomerAutomaticReminderPreference NÃO foram aplicadas a agendamento_local;
ambas são necessárias antes de iniciar a versão nova. API PID 27004/porta 5050 preservada.
Sem mudança de segredos/cadastros/banco de trabalho, commit/push/deploy ou entrega real.

Contrato HTTP permaneceu igual; snapshot OpenAPI PASS no verify completo. Frontend/schema
e mudanças anteriores preservados. Build Release zero avisos/erros e EF sem pending changes.
As 15 features finais tiveram motor=0/runner=0, 224 resultados por execução (218 PASS, zero
falhas e seis skips antigos), com TRX/JSON/log separados em .local-verification/daily-reminders/
final/<feature>/. Gate global exit 0: zero erros/avisos e 95/95 critérios provados.
Relatório .spec/audit-aviso-diario.json; revisão .spec/revisao-aviso-diario.md.
T-105 concluída após verify; plano regenerado sem executar headless/modelos históricos.

T-023 revisada em .spec/revisao-t023-diario.md: divergências reais de erros, respostas vazias,
metadata ausente e downloads PDF/CSV continuam pendentes. O gate mecânico não certifica esses
casos ainda sem provas abrangentes. Não encerrar T-023/tarefas históricas por aparência,
nem avançar para telas financeiro/equipe ou e-mail real nesta etapa.

## Retomada do contrato HTTP/OpenAPI — 12/10/2026

T-023 executada manualmente no workspace atual, mantendo main/96875bb e master/ca1d485,
modelo/esforço e todas as alterações locais anteriores. Agentes auxiliares herdaram a mesma
configuração. Nenhum commit/push/deploy, executor headless de modelo ou rollout de migrations.

Revisão cobre todas as 50 operações (incluindo /health). Erros de clientes, notificações,
orçamentos, agenda, serviços e OS têm ProblemDetails seguros coerentes com status/mídia;
401 de challenge JWT é vazio e documenta WWW-Authenticate sem detalhes internos. 403 de
permissão é ProblemDetails real. 204/404 e pagamentos 200 intencionalmente vazios não têm
content no contrato. Binding 400/415 e erro inesperado 500 têm corpo produzido explicitamente
pelo runtime, inclusive em Development; transformer não inventa corpo para erros vazios.
CNPJ tem 429 real, confirmação de e-mail 400 documentado e sessão 503 seguro em indisponibilidade.
Assinatura ausente e plano inexistente têm 404 seguro; webhook inválido 400 e sucesso 200 vazio.
Itens null de orçamento são recusados antes de mutações; orçamento salvo permanece intacto.

PDF/CSV possuem mídia, schema de arquivo e headers de download, com bytes/nome/conteúdo
real comprovados por HTTP. PdfPig 0.1.16 é dependência somente de testes. A prova semântica
detectou defeito real do renderer instalado: 48/400 PDFs concorrentes tinham mapas ToUnicode
corrompidos; 400 sequenciais passavam e pypdf confirmou os caracteres NUL. Geração nativa
agora usa SemaphoreSlim estático, compartilhado entre instâncias, espera cancelável e finally.
400 gerações concorrentes corrigidas passaram, sem corrupção; regressão de 24 downloads HTTP
concorrentes distintos verifica documento/cliente/serviço/total, bytes, headers e nomes.
Limitação deliberada: cada processo renderiza um PDF de cada vez; layout e dados não mudaram.

OpenAPI exportado por host InMemory Release/UseAppHost=false e schema frontend regenerado
pelo arquivo. Frontend de conversão consome title ProblemDetails, aceita 401 vazio e valida
workOrderId; atualização de status OS serializa enum numérico conforme schema. Documentação
instalada Next.js 16.2.11 lida antes de alterações. Frontend: 225 testes em 48 arquivos PASS,
typecheck, build e lint dos arquivos editados PASS; typecheck renovado após a exportação final.

Provas e falhas intermediárias preservadas em .local-verification/t023/ e t023-pdf-*.
O audit intermediário saiu 0 mesmo com um runner=1: NÃO foi aceito como certificação.
Coletor de testes agora registra stdout de cada suíte, usa builds exclusivos e não aborta
o JSON granular atual por stderr nativo de teste falho no Windows PowerShell. Renovação final
em .local-verification/t023/render-certified/<feature>/; cada execução exige motor=0/runner=0.
Detalhes e estado final na revisão .spec/revisao-t023.md e no relatório .spec/audit-t023.json
do fechamento. T-023 concluída: 15 features com motor=0/record.exitCode=0, 250 resultados
por execução, 244 PASS, seis skips antigos e zero falhas. Audit --ci exit 0, zero erros/
avisos, 95/95 critérios provados. Regeneração sequencial recusada pelo motor porque todas
as tarefas da feature já estão concluídas; planos/executor anteriores preservados como
históricos obsoletos, sem execução headless ou adoção de modelos históricos.

Banco/cadastros/segredos de trabalho e backup runtime-repair/agendamento_local-before-rls.dump
preservados. Nenhum comando encerrou ou reiniciou a API do usuário; na conferência final,
PID 27004 não estava ativo e não havia listener TCP em 5050. Causa não determinada.
AddCustomerAutomaticReminderPreference
e 20261012191900_AddDailyReceivableReminderDeduplication NÃO foram aplicadas a agendamento_local;
ambas são necessárias antes de iniciar versão nova nesse banco. Sem entrega real de e-mail/
WhatsApp e sem início de telas financeiro/equipe. Tarefas históricas restantes continuam pendentes.

## Modelo de serviços e orçamentos — T-031 — 12/10/2026

Execução manual, modelo/esforço atuais e dois agentes auxiliares herdando configuração. HEADs conferidos be79d2c/main (backend) e cf00e8c/master (frontend), posteriores às bases informadas 96875bb/ca1d485; sem commit/push/deploy. Alterações preexistentes, obj, schema, sinais, relatórios/TRX, PDF serial/semântico e backup do banco preservados.

T-031 concluída após revisão de ServiceItem, Quotation, QuotationItem, QuotationStatus, DepositInfo e DepositType. Tenant obrigatório nas factories; catálogo validado no tenant também na criação. Enum de sinal desconhecido recusado. AddItem/RemoveItem validam candidatos/totais/entrada antes de mutar, preservando agregado em falhas. Edição pendente e estados finais mantidos.

Q-020/Q-021 respondidas pelo usuário: entradas monetárias, percentual e quantidade até duas casas; teto 9.999.999.999.999.999,99; quantidade positiva; subtotal arredondado por item e sinal percentual arredondado com meio para longe de zero; saldo deriva da entrada já arredondada. Sem migration. Financeiro usa RequiredAmount do orçamento; resposta OS e prévia frontend usam subtotal arredondado. Frontend calcula centavos em bigint. Contrato number mantém limitação preexistente de exatidão em extremos JavaScript, documentada no design/revisão. Catálogo UI mantém restrição histórica de preço positivo enquanto domínio aceita zero; não certificar UI de catálogo por esta tarefa de modelo.

Novas provas: 14 casos unitários e dois HTTP/PostgreSQL temporário/RLS, incluindo limites, frações, recusas sem mutação/persistência, catálogo externo e recebíveis conciliados. Focados finais: domínio 23 PASS, HTTP dois PASS/exit0. Frontend 232 testes/49 arquivos, typecheck, lint relevante e build exit0. OpenAPI reexportado por host InMemory e schema regenerado pelo arquivo, com conteúdo final inalterado.

As 15 features tiveram motor=0 e record.exitCode=0: 266 resultados, 260 PASS, zero falhas, seis skips antigos por execução. TRX/JSON/log preservados por feature em .local-verification/t031/certified; contadores em trx-summary.json e códigos em summary.json. Revisão .spec/revisao-t031.md; novo design.md na feature; gate final em .spec/audit-t031.json.

T-088 compartilha entidades mas permanece pendente; T-032 a T-037 e T-089 a T-092 também não concluídas automaticamente. Plano sequencial regenerado para 11 tarefas restantes, apenas artefato informativo: executor não iniciado e modelos históricos não adotados. Próxima retomada deve escolher tarefa específica e revisar provas próprias.

Banco/cadastros/credenciais locais intactos. Porta 5050 sem listener nas conferências; nenhum processo do usuário iniciado/encerrado. AddCustomerAutomaticReminderPreference e 20261012191900_AddDailyReceivableReminderDeduplication continuam NÃO aplicadas a agendamento_local; não iniciar versão nova nesse banco antes de rollout autorizado. Sem entrega real WhatsApp/e-mail ou telas financeiro/equipe.

Gate T-031: audit --ci --json exit 0, ok=true, zero erros/avisos, 95/95 critérios provados (.spec/audit-t031.json). Uma suposição aberta global preexistente permanece; nenhuma pergunta em aberto. Diff check dos dois repos exit0. Conferidor adicional teve incompatibilidade de array JSON no Windows PowerShell5 e contador notExecuted do VSTest; corrigido para ler a coleção e os resultados reais, sem alterar provas/asserts/motor. As 15 execuções de testes propriamente ditas não falharam.

## Entidades e ciclo de vida do orçamento — T-088 — 12/10/2026

Retomada manual em backend main/977d415 e frontend master/3d8b597, modelo/esforço mantidos; dois auxiliares herdaram configuração. Cinco obj backend e .codex-checks frontend preexistentes preservados. Revisão própria das cinco entidades, Draft interno/Pending público, CustomerId/TenantId obrigatórios, itens, entrada e todas as transições; sobreposição de cálculos com T-031 e edição com T-103 registrada em .spec/revisao-t088.md.

Lacuna corrigida: handler de status executava CheckExpiration antes de recusar uma decisão, alterando entidade rastreada embora não persistisse. Validação agora antecede mutação; recusa mantém snapshot, EntityState.Unchanged e SaveChanges posterior sem alteração. Cancelamento de vencido permanece permitido. Contrato HTTP/OpenAPI e frontend não mudaram.

Novos testes focados: 53 PASS unitários e duas provas HTTP/InMemory/PostgreSQL temporário RLS, todos exit0/zero falhas/skips. Matriz de transições, guardas/expiração, coleção/associações, idempotência Approved/Converted, cliente externo/anônimo, isolamento/PDF/conversão externos e persistência sem registros parciais. Evidências em .local-verification/t088/domain e http; renovação das 15 features em certified, preservando cada TRX/JSON/log e conferindo motor e record.exitCode separadamente.

Q-022 pergunta necessária: AC-093 exige recusar aprovação/rejeição repetidas, mas handler aceita aprovação idempotente de Approved/Converted sem duplicar recebíveis. Domínio continua estrito. Usuário respondeu Q-022: documentar exceção idempotente para aprovação pública repetida, mantendo rejeições repetidas bloqueadas. AC-093 atualizado, idempotência preservada. As 15 features renovadas tiveram motor=0/record.exitCode=0, 321 resultados (315 PASS, zero falhas, seis skips históricos). T-088 concluída após revisão/provas/audit --ci exit0 (95/95, zero erros/avisos), com relatório final .spec/audit-t088.json; tarefas T-089 a T-092 e T-032 a T-037 permanecem pendentes.

Sem commit/push/deploy, executor headless, alterações no banco/segredos/cadastros de trabalho, nova migration ou processo na porta5050. Backup runtime-repair preservado. AddCustomerAutomaticReminderPreference e AddDailyReceivableReminderDeduplication continuam NÃO aplicadas a agendamento_local; não iniciar versão nova nesse banco. Sem entrega real WhatsApp/e-mail nem telas financeiro/equipe.

## Mapeamento EF Core de orçamentos — T-089 — 13/10/2026

Retomada manual no backend main/dff01ae e frontend master/3d8b597; modelo/esforço atuais,
auxiliares herdados autorizados. AGENTS/skill/contexto/RLS/revisões T-031/T-088/spec/design/tasks
lidos. Alterações locais, cinco obj, frontend .codex-checks/schema, sinais/relatórios/evidências
anteriores e backup runtime-repair preservados. Sem commit/push/deploy, executor headless,
processo na porta5050 ou alterações de banco/cadastros/credenciais de trabalho.

T-089 concluída após revisão própria do modelo efetivo e roundtrip integral. Corrigidos filtro
EF de QuotationItem shadow tenant, guarda sync/async de itens Added/Modified/Deleted e guarda
owned DepositInfo mesmo com Quotation Unchanged (valida owner/tenant original/current).
Coleção usa explicitamente backing field. Domínio/estados/regras monetárias/RequiredAmount,
permissões/CPF, concorrência financeira/notificações/PDF serial e contrato HTTP preservados.

Lacuna física real: catálogo só era validado pelos handlers, sem FK. Nova migration
20261013173339_EnforceQuotationCatalogTenantReference acrescenta AK ServiceItems(tenant_id,Id),
índice e FK composta de item/catalogo Restrict; ServiceItemId null continua avulso. Upgrade
válido preserva todos os campos; externo/órfão causa rollback de dados/history, sem limpeza.
Status enum e itens numeric sem typmod continuam históricos; domínio valida dinheiro/quantidade.
Snapshot consistente, HasPendingModelChanges=false; FKs compostas cliente/agregado históricas
instaladas por SQL examinadas separadamente do modelo. Ver .spec/revisao-t089.md e design.md.

32 provas metadata/EF/guardas e sete PostgreSQL reais novas PASS/exit0, zero skips/falhas.
Bases e logins restritos exclusivos; roundtrip inclui todos estados/campos, sinal null/zero/pago,
teto monetário, coleção após materialização, EF/RLS/IgnoreQueryFilters/SQL, vínculos externos,
NOTNULL sem RLS mascarar prova, unicidade, cascade/restrict e migrations válidas/inválidas.
Primeira regressão motor0/record1 foi recusada (10 falhas de fixtures InMemory sem guarda).
Somente preparação das fixtures foi corrigida; TODAS asserções/filtros preservados. Falhas e
logs/TRX/JSON completos mantidos em .local-verification/t089/failed-global-01 e rodadas focadas.

Renovação final das 15 features: motor0/record.exitCode0 em todas, 360 resultados por execução,
354 PASS, zero falhas e seis skips antigos; provas em .local-verification/t089/certified.
Audit --ci exit0, 95/95 critérios provados, zero erros/avisos (.spec/audit-t089.json).
T-090 a T-092 e T-032 a T-037 permanecem PENDENTES; T-031/T-088/T-103 continuam concluídas.
T-032 tem sobreposição, mas sua revisão própria catálogo/migrations/AC-045 não está encerrada.
Concorrência integral da conversão permanece sem certificação por T-089. Plano sequencial para
nove tarefas regenerado somente como artefato; executor não iniciado/modelos históricos ignorados.

AddCustomerAutomaticReminderPreference, AddDailyReceivableReminderDeduplication e a nova
EnforceQuotationCatalogTenantReference NÃO foram aplicadas a agendamento_local. Não iniciar
esta versão nem aplicar migrations nesse banco sem rollout autorizado/revisão do legado.
Frontend e schema intactos; HTTP/OpenAPI não mudou, sem regeneração necessária. Nenhuma entrega
real WhatsApp/e-mail ou tela financeiro/equipe. Próxima tarefa deve receber revisão própria.
