# Revisão T-091 — Transformar Orçamento em O.S. — 14/10/2026

## Bases e preservação

Execução manual em backend main/d66266f e frontend master/6f1f557, upstreams sincronizados (0/0), modelo/esforço atuais e três auxiliares herdados autorizados. AGENTS de ambos, skill local, continuidade/RLS, revisões/audits T-023/T-031/T-088/T-089/T-090 e spec/design/tasks lidos antes de alterar. Sem executor headless, commit/push/deploy, API5050 ou alteração no banco/cadastros/segredos de trabalho.

Cinco obj copiados com hashes em .local-verification/t091/preexisting e obj-baseline.json. Verificação/sinais anteriores copiados; 25.600 arquivos das provas T-090 certified/recertified, backup runtime-repair, .codex-checks e schema frontend registrados em preserved-baseline.json. Schema anterior também em frontend .codex-checks/t091/schema-before.d.ts; OpenAPI anterior em t091/preexisting/openapi-v1.json. Outputs Release/UseAppHost=false, logs/TRX/JSON exclusivos por rodada. Fixtures leem segredos apenas em memória e criam bancos/logins restritos temporários; não imprimem conexões, senhas ou tokens. Porta5050 inicialmente sem listener.

## Escopo e contratos

Handler/Command, WorkOrder/itens, MarkAsConverted, UoW, EF/modelo/snapshot/integridade física, Program/DI e ambas rotas revisados contra US-047, AC-091/093/092 e T-023. Rotas/DI já existiam; notas históricas de órfãos não são diagnóstico atual. Handler transient usa DbContext/UoW scoped por requisição.

Conversão padrão conserva200/{workOrderId}; alternativa conserva201/{id}/Location, inclusive replay. Ambas exigem autenticação/sales.write e tenant exclusivamente da identidade. Não há body binding: JSON malformado/mídia enviados não são comandos nem autoridade de tenant. UUID malformado404 vazio; vazio/inexistente/externo400 seguro indistinguível dentro de cada rota; falhas inesperadas500 ProblemDetails seguro.

Somente Approved cria uma nova OS; replay devolve a existente sem mutação/duplicação. Scheduled/CreatedAt UTC/CompletedAt null aguardam início manual, sem iniciar execução ou criar agendamento. Copia tenant, cliente, orçamento, códigoOS-, notas e todos campos dos itens, com IDs próprios. Emissão/validade/termos/sinal permanecem no orçamento vinculado; recebíveis existentes não são gerados/recalculados pela conversão.

T-092 continua integração geral; T-034 trata orquestração/cálculos/status; T-036 inclui catálogo/PDF; T-037 integração/PDF; T-103 mantém edição Pending. Nenhuma dessas tarefas é concluída automaticamente.

## Lacunas corrigidas

- Precheck da OS não tinha transação/lock, e CommitAsync sem Begin era no-op. Agora Begin, FOR UPDATE da linha de orçamento com tenant explícito antes das leituras, replay/validação/staging/save/commit; catch faz rollback com CancellationToken.None. O lock é compartilhado com edição/aprovação. Índice único físico/modelo em (tenant_id,QuotationId) fornece barreira adicional.
- Orçamento válido Code50 + prefixo OS- e Notes2000 excedia OS50/1000; nomes/unidades text excediam OS255/50. Migration20261013202849_EnforceWorkOrderConversionIntegrity amplia53/2000/text, sem truncar dados nem restringir entrada pública.
- WorkOrderItems não tinha FK de catálogo. Nova FK composta nullable (tenant_id,ServiceItemId) → ServiceItems(tenant_id,Id), Restrict e índice. FKs compostas históricas OS/cliente/orçamento e item/owner, filtros/RLS permanecem. Upgrade com duplicidade/catalog externo/inexistente falha atomicamente, sem limpeza de legado.
- Guarda owned não validava Modified/Deleted quando owner permanecia Unchanged; Added shadow era preenchido cegamente. Agora valida owner/contexto e tenants original/current, inclusive shadow, antes do preenchimento, nos caminhos sync/async. EF não admite queryfilter root separado para owned; filtro do owner e RLS protegem sua leitura.
- WorkOrderItem passa a validar precisão/teto/quantidade/desconto com QuotationMoney, inclusive desconto contra subtotal arredondado. Q-020/Q-021 e RequiredAmount preservados.
- Location201 observado da alternativa estava ausente no OpenAPI. Metadata o documenta sem mudar respostas. Documento exportado pelo host InMemory e schema frontend regenerado pelo arquivo local, sem API5050.

## Provas próprias

QuotationConversionAtomicityTests:8/8 PASS, exit0, zero falhas/skips em t091/unit/run1. Matriz de recusas Draft/Pending/Rejected/Expired/Canceled/Converted-semOS preserva snapshot, identidades rastreadas, EntityState.Unchanged, SaveChanges posterior0 e persistência; ausência de tenant não carrega/estagia. Inclui precisão/teto/desconto/zero dos itens de OS.

Foco conjunto:30/30 PASS, exit0, zero falhas/skips em t091/focused/run3: HTTP2, concorrência13, integridade14 e contrato1. HTTP compartilhado InMemory/PostgreSQL restrito comprova duas rotas, autenticação/permissões/spoof/isolamento, IDs/estados, bodies/mídias/headers/ProblemDetails, cópia Code50/Notes2000/Name300/Unit60, números/zero/desconto/sinal, vínculos/Scheduled e replay com recebíveis intactos. Incremento final prova início/conclusão manuais seguidos de replay nas duas rotas preservando status/notas/CompletedAt; renovação global valida esse incremento.

Concorrência: três pares (padrão/padrão, alternativa/alternativa, cruzadas), duas interações com aprovação repetida/edição/rejeição/cancelamento e duas esperas por aprovação inicial/edição Pending. Observador exige duas ou cinco sessões distintas simultaneamente bloqueadas antes de liberar a linha, com pool suficiente. Sete JSON preservados em t091/concurrency/run3/concurrency; caminho final dos observadores é t091/concurrency. Resultados exigem mesmoID/umaOS/itens exatos/Converted/recebíveis intactos, e conteúdo preservado nas decisões recusadas.

Rollback: seis casos (duas rotas × AFTER INSERT OS/item e AFTER UPDATE orçamento). Trigger lança P0001 após write; sequência não transacional prova que houve escrita antes da falha. HTTP500 seguro, orçamento Approved com snapshot completo, títulos intactos, zero OS/itens parciais; retry após retirar trigger cria OS válida. Recusa de validação antes de mutar é distinta desse rollback relacional.

Integridade: metadata/snapshot HasPendingModelChanges=false; SQL restrito verifica unique23505, catálogo externo/inexistente23503, Restrict23001, null/local e RLS inclusive IgnoreQueryFilters; upgrade válido preserva todos campos e três upgrades inválidos preservam linhas/schema/history. Guardas owned sync/async incluem adulteração original/current de item/owner. Asserções cronológicas de T-089 atualizadas com IDs pendentes exatos e Contains catálogo/Last migration nova, mantendo exigências anteriores.

Revisão auxiliar final somente leitura não encontrou bloqueadores. Estados/expiração/cancelamento, validação antes de mutar, Q-022/idempotência financeira, CPF/PreserveDocument, notificações/opt-out/aviso diário e PDF serial/semântico/concorrente permanecem sob regressão.

## Tentativas e limites

HTTP run1: InMemory PASS; PostgreSQL falhou na preparação PendingModelChanges durante alteração paralela de config/migration. focused/run1: buildexit1 xUnit2031, corrigido overload Single(predicate). focused/run2:19PASS/6FAIL; expectativa SQL Restrict23503 corrigida para23001 mantendo constraint; cinco observadores mantinham snapshot pg_stat congelado dentro da transação Admin. Observador agora executa pg_stat_clear_snapshot por amostra, mantendo a exigência de sessões simultâneas. focused/run3:30PASS. Logs/TRX anteriores mantidos. JSON inicialmente gravados na cwd output/bin foram preservados e copiados; o caminho foi corrigido para a raiz do repo.

Rollback restaura linhas do banco, não valores do ChangeTracker. Scope HTTP é descartado após falha relacional; SaveChanges posterior0 refere-se apenas às recusas antes de mutar. Handler não certifica composição com transação externa já aberta nem reuso manual de agregado previamente rastreado/stale. O contrato exercitado usa scopes novos por requisição.

## Gate e operação

Renovação final das 15 features concluída: motor=0 e record.exitCode=0 em todas, 406 resultados por execução (400 PASS, zero falhas e seis skips históricos). Cada TRX/JSON/log foi preservado antes da execução seguinte; índice em .local-verification/t091/final-certified-summary.json. Audit --ci exit0, ok=true, 95/95 critérios provados, zero erros/avisos em .spec/audit-t091.json. Somente T-091 concluída após revisão própria e provas abrangentes; T-092 e T-032 a T-037 continuam pendentes.

Migration nova não aplicada a agendamento_local; também continuam não aplicadas AddCustomerAutomaticReminderPreference, AddDailyReceivableReminderDeduplication e EnforceQuotationCatalogTenantReference. Não iniciar esta versão nem aplicar migrations nesse banco sem rollout explícito. Backup agendamento_local-before-rls.dump preservado. Sem entrega real WhatsApp/e-mail ou telas financeiro/equipe.
Correção de preparação na regressão completa: primeira rodada cadastro-tenants teve motor0/record1 (405 resultados:398PASS/1FAIL/6skips) por ManyServiceProvidersCreatedWarning, pois cada host InMemory criava uma raiz exclusiva. Fixture TeamFinancialTestFactory agora reutiliza uma raiz estática, conservando nome de banco exclusivo por host. Nova prova de hosts independentes no mesmo tenant mantém200 no host proprietário e404 no outro. A primeira execução dessa prova faltava cliente referenciado; fixture corrigida com cliente real, sem alterar asserções. Segunda rodada recusada (406 resultados:399PASS/1FAIL/6skips) preservada em certified-final; foco de contrato/isolamento posterior2/2PASSexit0 em fixture/run1. Renovação aceita deve estar em certified-complete, com406 resultados/400PASS/zero falhas/6skips por feature. Nenhum warning/teste foi suprimido.
## Fechamento T-091 — 14/10/2026

Somente T-091 concluída após revisão própria, 39 casos novos e certificação completa. As 15 features renovadas têm motor=0 e record.exitCode=0, cada uma com406 resultados (400PASS, zero falhas e seis skips antigos). Logs/TRX/JSON preservados em .local-verification/t091/certified-complete; índice final em final-certified-summary.json. Audit --ci exit0, ok=true,95/95 critérios provados, zero erros/avisos (.spec/audit-t091.json).

Frontend: schema regenerado pelo OpenAPI local após documentar Location201 da rota alternativa; documentação instalada Next.js16.2.11 consultada. Typecheckexit0 e232/232 testes PASS, sem alteração de componentes/BFF. Exportação OpenAPI hostInMemory exit0/1PASS; contrato observado200/workOrderId e201/id/Location preservado, inclusive replay.

T-092 e T-032 a T-037 permanecem pendentes; T-031/T-088/T-089/T-090/T-103 mantêm conclusão. Plano sequencial para sete tarefas regenerado somente como artefato; executor não iniciado e modelos históricos não adotados. Sem commit/push/deploy, serviço5050, entrega real WhatsApp/e-mail ou telas financeiro/equipe. Nova migration não aplicada ao banco de trabalho; quatro migrations continuam exigindo rollout explícito. Ver .spec/revisao-t091.md e docs/POSTGRESQL_RLS.md.