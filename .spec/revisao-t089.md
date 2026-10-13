# Revisão T-089 — Mapeamento EF Core para Orçamentos — 13/10/2026

## Base e preservação

Execução manual com modelo/esforço atuais e dois auxiliares herdando configuração. Backend main/dff01ae e frontend master/3d8b597 confirmados por status/branch/log. Cinco obj backend e .codex-checks frontend preexistentes preservados; baseline/cópias em .local-verification/t089/preexisting, inclusive verificação/sinais. Hashes de schema/frontend, evidências T-088 certified e backup runtime-repair registrados. AGENTS dos dois repos, skill local, continuidade/RLS, revisões/audits T-031/T-088 e spec/design/tasks lidos antes das alterações. Nenhuma lição confirmada existente.

Sem commit/push/deploy ou executor headless; nenhum serviço iniciado na porta5050. Sem alterações de banco/cadastros/credenciais de trabalho. Segredos somente lidos pelas fixtures em memória, nunca impressos. Output de testes Release/UseAppHost=false exclusivo; geração EF em cópia isolada dos fontes.

## Revisão própria e sobreposição

T-089 cobre US-045/AC-092: configuração efetiva após convenções, entidades Quotation/QuotationItem/DepositInfo, roundtrip, vínculos/índices/exclusões/integridade, filtros/guarda/RLS e migrations descobertas/snapshot/diferenças. T-032 compartilha DbContext/configuração, mas inclui catálogo e migrations como entrega própria/AC-045; continua pendente. T-090 a T-092 e T-033 a T-037 não são certificadas automaticamente; concorrência integral da conversão segue sem certificação própria.

Quotation: PK Id UUID, TenantId/CustomerId/Code/IssueDate/Status/totais obrigatórios; Code50, Notes/PaymentTerms2000; validade/notas/termos opcionais. Status integer mantém valores históricos; não há CHECK de enum no banco. Datas timestamptz; totais numeric(18,2). Índices tenant/código único, tenant/status, tenant/cliente e tenant; Customer Restrict, Items Cascade. Backing field _items explicitado para acesso EF sem setter público da coleção.

QuotationItem: PK Id, QuotationId e tenant_id obrigatórios; ServiceItemId nullable para itens avulsos, nome/unidade obrigatórios. Decimais numeric sem typmod, conforme migration/snapshot histórico; Q-020/Q-021 são validados pelo domínio (duas casas/teto/quantidade positiva/arredondamento), não por CHECK SQL. Não converter essa diferença consciente em migration cosmética.

DepositInfo: owned opcional compartilha Quotations e PK/linha do owner; não é tabela separada. Ausência colunas físicas null; presença materializa inclusive valores zero/false/notas null. Value/RequiredAmount/RemainingBalance numeric(18,2), notas1000, Type integer e IsPaid boolean. Todas as propriedades têm prova roundtrip, incluindo IsPaid true, sinal zero, fixo/percentual, saldo e ausência.

FKs compostas cliente/orçamento e item/orçamento existem fisicamente pela SQL histórica RLS, adicionalmente às FKs simples do modelo/snapshot; diferença examinada, sem reescrever histórico. ENABLE/FORCE RLS e login restrito continuam barreira independente da aplicação.

## Lacunas corrigidas

1. QuotationItem tinha TenantId shadow físico, mas não filtro EF. Agora consultas diretas de itens recebem filtro por contexto/tenant, inclusive ausência de contexto; IgnoreQueryFilters não remove RLS.
2. Guarda tratava somente Added shadow e ignorava Modified/Deleted de itens independentes. Agora compara tenant original/current e recusa tenant externo/contexto ausente antes de preencher shadows. Escrita recusada não grava operações válidas enfileiradas junto.
3. Escrita somente no sinal owned podia deixar Quotation Unchanged e escapar da guarda. Agora valida owner rastreado e tenant original/current; owned sem owner é recusado. Escritas próprias válidas e exclusão do sinal permanecem possíveis.
4. ServiceItemId não tinha qualquer FK; handlers validavam catálogo, mas SQL direto aceitava externo/órfão. Nova migration 20261013173339_EnforceQuotationCatalogTenantReference acrescenta AK ServiceItems(tenant_id,Id), índice e FK composta tenant/catalogo Restrict. Null continua permitido; desativação não muda. Exclusão de catálogo referenciado é recusada. Upgrade com legado inválido falha transacional, sem limpar dados ou registrar migration aplicada.

Migration gerada pelo EF; snapshot/designer correspondem ao modelo final e HasPendingModelChanges=false. Descoberta da cadeia confirma AddQuotationsAndServices, EnforceTenantRowLevelSecurity e nova migration; AddRowLevelSecurity antigo continua não descoberto/ativado. Nenhuma migration aplicada a agendamento_local.

## Provas próprias e revisão

QuotationMappingTests: 32/32 PASS, exit0, zero falhas/skips, .local-verification/t089/mapping-final/results/mapping.trx e test-result.json. Metadata efetiva Npgsql/designtime, colunas/keys/indexes/owned/lengths/field access/enum/precisão, snapshot, filtros sem tenant/consulta item, guarda sync/async para Modified/Deleted/Added shadow foreign/adulterado, owner Unchanged e owned sem owner, escrita válida e ausência de gravação parcial.

QuotationPersistenceTests: 7/7 PASS, exit0, zero falhas/skips, .local-verification/t089/persistence-complete.log e agent-results/persistence-complete.trx. PostgreSQL exclusivo com login app restrito: todos campos/estados/itens/sinal null/zero/pago e teto monetário; materialização/alteração de coleção; filtros e RLS inclusive IgnoreQueryFilters/SQL direto e alteração owned externa; referências cliente/agregado/catálogo externas e inexistentes; NOT NULL separado da recusa RLS; unicidade por tenant; Restrict e Cascade real; ausência de mutação; upgrade válido compara JSON de todos campos históricos; upgrades externo/órfão com rollback de dados e history.

Revisão auxiliar final somente leitura não encontrou defeitos bloqueantes. Regra Draft interno/Pending público, estados/expiração/cancelamento histórico, validação antes de mutar, RequiredAmount, idempotência financeira/conversão, permissões, CPF/PreserveDocument, notificações/opt-out/aviso diário, PDF serial/semântico e HTTP/OpenAPI preservadas. Nenhum DTO/rota/resposta alterado; schema frontend intacto, sem necessidade de regeneração ou alteração Next.js.

## Falhas intermediárias preservadas

Geração em scratch inicialmente pediu restore (NETSDK1004); tentativa de configurar EntityTypeBuilder.Model falhou CS1061, corrigida usando ModelBuilder no DbContext. Logs migration-generation/migration-build/restore preservados; geração final exit0. Metadata ampliada teve xUnit2031 de overload LINQ, corrigida com Assert.Single predicate sem afrouxar assert.

Rodadas focadas PostgreSQL iniciais foram preservadas: preparação do item novo precisava db.Add como handler, códigos específicos RestrictViolation23001 versus ForeignKeyViolation23503 e RLS42501 versus NOTNULL23502, e remoção de item precisava selecionar por identidade/referência, pois ordem da coleção materializada não é garantida. Prova owner separada confirma NOTNULL sem RLS mascarar a restrição. Sinal removido antes de excluir item que reduziria total abaixo da entrada fixa. Nenhuma asserção preexistente removida/enfraquecida.

Primeira renovação global: motor0, record.exitCode1, 360 resultados/344PASS/10FAIL/6skips; NÃO aceita como certificação. Falhas eram fixtures antigas InMemory sem interceptor deixando shadow tenant vazio. Somente setup de TeamFinancialTestFactory.Db e QuotationStatusAtomicityTests.CreateContext foi atualizado para usar guarda com mesmo tenant; todas assertions e filtros mantidos. Foco atomicidade renovado5/5PASS/exit0. Evidência global completa preservada em .local-verification/t089/failed-global-01 antes da execução seguinte.

## Fechamento

Renovação global final concluída: 15/15 features com motor=0 e record.exitCode=0, 360 resultados por execução (354 PASS, zero falhas e seis skips históricos). Logs/TRX/JSON preservados antes da seguinte execução em .local-verification/t089/certified; summary.json e check-final.ps1 conferem resultados reais e códigos separadamente. Audit de revisão --ci exit0: ok=true, 95/95 critérios provados, zero erros/avisos. Somente T-089 marcada concluída após revisão própria e provas abrangentes. Plano sequencial regenerado para nove tarefas restantes; executor não iniciado, modelos históricos não adotados. Gate final em .spec/audit-t089.json. Uma suposição global aberta preexistente permanece; nenhuma pergunta aberta.

AddCustomerAutomaticReminderPreference, AddDailyReceivableReminderDeduplication e a nova EnforceQuotationCatalogTenantReference NÃO foram aplicadas ao banco de trabalho. Não iniciar esta versão em agendamento_local antes de rollout autorizado e revisão do legado. Sem entrega real de WhatsApp/e-mail ou telas financeiro/equipe.

Conferência final: hashes dos cinco obj, backup e evidências T-088 certified preservados; versões anteriores de sinais/verificação copiadas na baseline. 37 arquivos frontend/schema conferidos intactos. HEADs dff01ae/main e 3d8b597/master mantidos, diff --check exit0 nos dois repos, nenhum listener5050. Evidência em .local-verification/t089/final-environment.json e preserved-evidence.json. Não houve commit/push/deploy.
