# Matriz de integração de orçamentos — T-092 — 14/10/2026

Esta matriz resulta da leitura dos testes, spec/design/tasks e revisões T-023/T-031/T-088/T-089/T-090/T-091. Cada linha identifica o cenário e a natureza da prova; presença de teste ou tag não substitui resultado PASS. A revisão e o gate finais de T-092 devem registrar a renovação efetivamente aceita. T-032 a T-037 permanecem pendentes e não são encerradas pela reutilização destas provas.

## Natureza das provas

| Camada | O que comprova | Limite |
| --- | --- | --- |
| Domínio puro | Cálculos, limites, transições, conteúdo preservado após recusa | Não comprova persistência, DI, HTTP, FK ou RLS |
| Handler direto + EF InMemory | Composição explícita, guarda de gravação, filtros EF, tracker, SaveChanges posterior e materialização em outro contexto | Não comprova transação/rollback relacional, locks, constraints ou RLS |
| HTTP + EF InMemory | Rotas, binding, autenticação, permissões, identidade tenant, DI/scopes e respostas reais do host de teste | Não comprova integridade ou concorrência PostgreSQL |
| HTTP + PostgreSQL restrito temporário | Contrato HTTP e persistência/materialização relacional sob RLS | Apenas as interações e concorrências explicitamente exercitadas |
| EF/SQL + PostgreSQL restrito temporário | Filtros e RLS independentes, integridade física, migrations, transações e locks observados | SQL bruto não passa pelas validações financeiras/de estado do domínio |

## AC-090 — criação Pending, vínculos e valores

| Cenário | Testes / camada | Evidência e abrangência |
| --- | --- | --- |
| Draft interno, associação obrigatória, montagem e publicação Pending | `QuotationDomainTests.ServiceItem_And_Quotation_ShouldImplement_ITenantOwned`, `MarkAsPending_WithoutItems_ShouldThrowException`; `QuotationLifecycleTests.DraftAssembly_PreservesIdentityAndPublishesPending`, `Factory_RejectsMissingAssociationsAndInvalidValidity` — domínio | Draft não é status da criação pública; identidade e conteúdo de montagem são verificados separadamente |
| Soma de itens, desconto, entrada fixa/percentual | `QuotationDomainTests.AddItem_ShouldRecalculateTotals`, `SetDeposit_Percentage_ShouldCalculateRequiredAmount`, `SetDeposit_FixedAmount_ShouldCalculateRemainingBalance` — domínio | Casos simples; precisão/teto/zero/arredondamento vêm da cobertura complementar abaixo |
| Duas casas, teto 9.999.999.999.999.999,99, quantidade positiva, preço/desconto zero, subtotal por item e percentual meio para longe de zero | `QuotationModelValidationTests.MonetaryInputBoundaries`, `ItemAndPercentageRoundingConserveMoney`, `InvalidDepositDoesNotReplaceExistingDeposit`, `RefusedItemChangesAreAtomic`; `QuotationModelHttpTests.MonetaryModelOverHttp`, `MonetaryModelOverPostgreSql` | Domínio + HTTP InMemory/PostgreSQL; reconciliação entrada/saldo e financeiro usando RequiredAmount; prova histórica T-031 |
| Criação publica Pending e realmente materializa tenant/cliente/itens/soma | `QuotationTests.AC090_CreateQuotation_StatusPending_CalculatesTotal` — handler direto/InMemory; `QuotationLifecycleHttpTests.LifecycleOverHttp`, `LifecycleOverPostgreSql`; `QuotationEndpointsHttpTests.InMemoryContract`, `PostgreSqlContract` | Fixture T-092 lê em contexto novo e verifica tenant shadow dos itens; HTTP comprova persistência com request/scope próprio |
| Criação → edição Pending → aprovação do conteúdo revisado | `QuotationIntegrationWorkflowTests.EditedQuotationJourneyInMemory`, `EditedQuotationJourneyPostgreSql` — HTTP | Cliente, código, emissão, validade, notas, termos, itens e sinal substituídos; item antigo desaparece; 10,03 × 1,50 arredonda 15,045→15,05, desconto 0,04 gera 15,01; entrada 50% vira 7,51 e saldo 7,50; item manual gratuito permanece; recebíveis usam cliente revisado e somam total |

## AC-091 — conversão, cópia e repetição

| Cenário | Testes / camada | Evidência e abrangência |
| --- | --- | --- |
| Pending recusado, Approved aceito; UoW e handler compartilham contexto | `QuotationTests.AC091_ConvertApprovedQuotationToWorkOrder` — handler direto/InMemory | `new UnitOfWork(db)` elimina scope/contexto divergente. Contexto novo vê uma OS Scheduled, vínculo tenant/cliente, item e orçamento Converted; replay retorna mesmo ID |
| Todas as origens recusadas sem staging/mutação e SaveChanges posterior zero | `QuotationConversionAtomicityTests.RefusedConversionDoesNotStageAnyMutation`, `MissingTenantCannotConvert` — handler direto/InMemory | Draft/Pending/Rejected/Expired/Canceled/Converted-sem-OS; distingue validação antecipada de rollback após write |
| Cópia integral, IDs próprios, vínculo catálogo, Scheduled e início manual, duas rotas e contrato | `QuotationConversionHttpTests` — HTTP InMemory/PostgreSQL; `QuotationIntegrationWorkflowTests.EditedQuotationJourneyInMemory`, `EditedQuotationJourneyPostgreSql` | Jornada T-092 converte conteúdo editado, compara todos campos dos itens e total calculado, mantém notas/vínculos; OS Scheduled/CompletedAt null. Rotas mantêm 200/workOrderId e 201/id/Location inclusive replay. T-091 também prova início/conclusão manual seguido de replay preservando OS |
| Replay sequencial/cruzado preserva uma OS, itens e recebíveis existentes | Workflow T-092; `QuotationConversionHttpTests`; `QuotationConversionConcurrencyTests.SimultaneousConversions`, `ConcurrentInteractions` | Workflow alterna ambas rotas quatro vezes; snapshots financeiros e da OS não mudam com novas decisões recusadas |
| Concorrência real entre conversões/decisões/edição e aprovação inicial | `QuotationConversionConcurrencyTests.InitialApprovalCommitsBeforeWaitingConversions`, `PendingEditCommitsBeforeRefusedConversions`, `SimultaneousConversions`, `ConcurrentInteractions` — HTTP/PostgreSQL | Observador exige duas ou cinco sessões PostgreSQL distintas simultaneamente bloqueadas antes de liberar linha; executa pg_stat_clear_snapshot entre amostras. Pool não é barreira probatória. Pares padrão/padrão, alternativa/alternativa e cruzado; resultado mesmo ID, uma OS/itens exatos/Converted/recebíveis intactos |
| Falha controlada depois de writes e rollback relacional | `QuotationConversionConcurrencyTests.RelationalFailureRollsBack` — HTTP/PostgreSQL | Duas rotas × AFTER INSERT OS/item e AFTER UPDATE orçamento. Trigger P0001 e sequência não transacional demonstram write anterior à falha; 500 seguro, orçamento Approved e títulos intactos, zero OS/itens parciais; retry cria OS após remover trigger. Não afirma restauração do tracker |
| Unicidade e referências físicas da OS/itens | `QuotationConversionIntegrityTests`, `QuotationPersistenceTests.ReferentialIntegrityAndDeleteRules` — PostgreSQL/SQL restrito | Unique tenant/orçamento, FKs compostas tenant/owner/cliente/orçamento/catálogo nullable Restrict; migrations válidas/invalidas e RLS independentes da aplicação |

## AC-092 — identidade, agregados e dependentes

| Cenário | Testes / camada | Evidência e abrangência |
| --- | --- | --- |
| Autenticação/permissões e tenant exclusivamente da identidade; spoof body/header/query | `QuotationEndpointsHttpTests.InMemoryContract`, `PostgreSqlContract`; `QuotationConversionHttpTests` | Matriz HTTP de operações e sales.read/sales.write, anonymous 401, forbidden 403, spoof sem autoridade. Conversão não tem body binding; conteúdo/mídia não representa comando tenant |
| Cliente/catalogo inexistente/externo recusados sem linhas parciais | `QuotationLifecycleHttpTests`, `QuotationModelHttpTests`, `QuotationEndpointsHttpTests`, `QuotationEndpointAtomicityTests` | HTTP/InMemory/PostgreSQL; validações antecipadas e constraints complementares; catálogo opcional/null permanece válido |
| Agregado e consultas diretas de itens isolados em contextos novos | `QuotationTests.AC092_TenantIsolation` — InMemory; Workflow PostgreSQL/InMemory | Novo tenant não vê orçamento/itens; host independente com mesmo tenant não recebe banco do host proprietário. Workflow não vê agregado/itens/OS/recebíveis estrangeiros e recusa GET/PUT/conversão externos |
| Filtros/guardas item e owned, tenants original/current/owner, sync/async | `QuotationMappingTests.FiltersApplyToAggregateAndDirectItemQueries`, `ForeignItemWritesRejectWithoutPersisting`, `NewItemRequiresAuthenticatedMatchingTenant`, `OwnedWithoutTrackedOwnerCannotWrite`, `ForeignOwnedWritesRejectWithoutPersisting`, `OwnOwnedWritesPersist`, `OwnItemWritesAndTampering` | EF InMemory valida proteção de aplicação; owner Unchanged não isenta owned; operações válidas enfileiradas não vazam junto com escrita recusada |
| RLS permanece ao remover filtro EF e SQL direto não atravessa tenant | `QuotationPersistenceTests.AggregateAndStandaloneDependentsAreIsolated`, `ReferentialIntegrityAndDeleteRules`, `UniqueCodeAndWriteGuards`; `QuotationConversionIntegrityTests` | PostgreSQL temporário/logins restritos; IgnoreQueryFilters, SQL direto, RLS FORCE/ENABLE e integridade física são provas distintas do filtro EF |
| Materialização integral e integridade/migrations | `QuotationPersistenceTests.CompleteAggregateRoundtrip`, `CatalogMigrationPreservesValidHistoricalData`, `CatalogMigrationRejectsInvalidHistoricalData`; `QuotationMappingTests.EffectiveMappingAndSnapshotRemainConsistent` | Todos estados/campos/itens/sinal null/zero/pago e teto; snapshot/modelo sem alterações pendentes; upgrades inválidos conservam dados/history |

## AC-093/Q-022 — decisões e idempotência

| Cenário | Testes / camada | Evidência e abrangência |
| --- | --- | --- |
| Domínio continua estrito: Approved/Converted não aprovam/rejeitam novamente | `QuotationDomainTests.StatusTransitions_ShouldFollowBusinessRules`; `QuotationLifecycleTests.EveryTransition_EnforcesItsSourceAndPreservesContent` — domínio | Não confundir factory/domain com exceção pública idempotente |
| Rejeitar Converted continua recusado | `QuotationTests.AC093_CannotUpdateStatusOfConvertedQuotation` — handler/InMemory | Asserção histórica preservada; título agora descreve somente rejeição, com tracker Unchanged, SaveChanges zero e nova leitura Converted/uma OS |
| Approved/Converted + Approved sucesso sem mutação; Approved/Converted + Rejected e Rejected + Approved/Rejected recusados | `QuotationTests.FinalDecisionsPreserveTrackedGraph` — seis casos handler/InMemory | Snapshot de valores persistentes, mesma contagem de entries, estado Unchanged, SaveChanges posterior zero, novo contexto conserva estado e contagens de OS/recebíveis |
| Aprovação pública repetida não duplica recebíveis/OS; rejeições repetidas bloqueadas | `QuotationLifecycleHttpTests.LifecycleOverHttp`, `LifecycleOverPostgreSql`; `QuotationEndpointsHttpTests`; Workflow T-092 | HTTP snapshots antes/depois, mesma OS e títulos; workflow conserva entrada/saldo e cliente revisado. Rejected/Canceled e vencimento cobertos pelos testes complementares |
| Recusa por vencimento não modifica tracker; cancelamento vencido permitido | `QuotationStatusAtomicityTests.ExpiredPending_RefusedDecisionCannotLeakMutationThroughLaterSave`, `ExpiredPending_CancellationRemainsAllowed`; `QuotationLifecycleHttpTests` | Handler/InMemory + HTTP/PostgreSQL; domínio usa fronteira UTC do dia. Não trata rollback de write como ausência de mutação rastreada |

## Índice de evidências e resultados

- T-092 focado disponível na leitura desta matriz: `.local-verification/t092/focused-04/results/focused.trx`, 12 resultados/12 PASS/zero falhas/zero skips (QuotationTests + Workflow). Este TRX não substitui a renovação global nem certifica sozinho QuotationDomainTests; a revisão final deve registrar seus resultados efetivos.
- T-031: `.local-verification/t031/unit-final`, `.local-verification/t031/http-agent`; certificação `.local-verification/t031/certified`, conforme `.spec/revisao-t031.md` (cálculos, limites, dinheiro/quantidade, HTTP/PostgreSQL).
- T-088: `.local-verification/t088/domain/results/domain-01.trx`, `.local-verification/t088/http/results`; global `.local-verification/t088/certified`, conforme `.spec/revisao-t088.md` (ciclo/transições/atomicidade e HTTP).
- T-089: `.local-verification/t089/mapping-final/results/mapping.trx`, `.local-verification/t089/mapping-final/test-result.json`, `.local-verification/t089/persistence-complete.log`, `.local-verification/t089/agent-results/persistence-complete.trx`; global `.local-verification/t089/certified`, conforme `.spec/revisao-t089.md`.
- T-090: `.local-verification/t090/http/results/round05.trx`, `.local-verification/t090/atomicity-r7`; global `.local-verification/t090/certified`, `.local-verification/t090/recertified`, `.local-verification/t090/final-certified-summary.json`, conforme `.spec/revisao-t090.md`.
- T-091: `.local-verification/t091/unit/run1`, `.local-verification/t091/focused/run3`, `.local-verification/t091/concurrency/run3/concurrency` e `.local-verification/t091/concurrency` (JSON observadores); global aceita `.local-verification/t091/certified-complete`, `.local-verification/t091/final-certified-summary.json`, `.spec/audit-t091.json`. Rodadas `certified-final` com falha não são certificação aceita. Conforme `.spec/revisao-t091.md`, global aceita tem 406 resultados/400 PASS/zero falhas/6 skips por execução, motor0 e record.exitCode0.
- T-023: `.local-verification/t023/render-certified`, `.spec/audit-t023.json`, conforme `.spec/revisao-t023.md`; contrato HTTP/OpenAPI, erros seguros, PDF serial/semântico/concorrente preservados. Não há mudança de contrato exigida pela matriz T-092.

As evidências históricas demonstram o escopo de cada revisão no momento respectivo; a renovação final T-092 deve conservar log/TRX/JSON por feature e verificar separadamente motor e record.exitCode, sem considerar exit0 global prova suficiente de cada cenário.

## Seis skips antigos — escopo e limite

| Arquivo | Casos pulados | Escopo |
| --- | --- | --- |
| `tests/Agendamento.IntegrationTests/Identity/UserPersistenceTests.cs` | 1 | Persistência identidade/Docker indisponível |
| `tests/Agendamento.IntegrationTests/WorkOrders/WorkOrderApiTests.cs` | 1 | Conversão alternativa histórica, AC-051/052, falha local |
| `tests/Agendamento.IntegrationTests/Tenancy/RowLevelSecurityTests.cs` | 1 | RLS histórico/Docker indisponível |
| `tests/Agendamento.IntegrationTests/Tenancy/MigrationSafetyTests.cs` | 1 | Inspeção histórica de migration/RLS, falha local |
| `tests/Agendamento.IntegrationTests/Persistence/PostgreSqlFixtureTests.cs` | 2 | Fixture PostgreSQL histórica/Docker indisponível |

Nenhum skip conta como prova de AC-090/091/092/093. Conversão e RLS são comprovados pelos testes reais posteriores citados acima; manter os skips históricos não atribui certificação a eles.

## Fixtures, contextos e fronteiras do escopo

QuotationTests passou de AgendamentoApiFactory/IntegrationTestDb compartilhado para TeamFinancialTestFactory: banco exclusivo por host, raiz InMemory compartilhada entre hosts para evitar ManyServiceProviders, interceptor tenant idêntico no seed/handlers e UoW sobre o mesmo DbContext. Contextos novos comprovam materialização e tenants shadow; host separado prova isolamento de fixtures sem suprimir warnings/filtros/asserts.

HTTP usa contexto novo por request. As recusas diretas que carregam o grafo antes da chamada são pertinentes para provar ausência de mutação rastreada e SaveChanges indevido; isso não certifica reutilização manual de contexto stale após alteração externa/concorrente. Tal reutilização não faz parte da jornada HTTP e pode exigir reload/descarte explícito; não é promessa deste conjunto.

Composição manual com transação externa já aberta também não é certificada: o handler/UoW gerencia sua própria transação no contrato exercitado. Nenhum teste deve ser interpretado como suporte a transações aninhadas/ambientais. Rollback relacional restaura linhas, não valores do ChangeTracker; scopes HTTP após falha são descartados. As observações de sessões bloqueadas e a falha após write são provas reais separadas da EF InMemory.

T-103 fornece edição Pending como interação necessária; esta matriz não reabre nem encerra sua revisão. T-032/T-033/T-034/T-035/T-036/T-037 continuam pendentes, com revisão própria de catálogo/migrations/orquestração/PDF/endpoints/integração. Nenhuma migration ou alteração no banco de trabalho é autorizada pela reutilização de provas temporárias.

## Renovação aceita T-092

As15features finais têm motor=0 e record.exitCode=0; cada execução lê414 resultados/408PASS/zero falhas/6skips antigos. QuotationDomainTests e todos os testes complementares foram efetivamente executados na suíte integral. Índice .local-verification/t092/final-certified-summary.json, logs/TRX/JSON por feature em certified e105observações novas PostgreSQL em postgresql-observations/index.json. Audit de revisão --ci exit0/95 critérios/zero erros e avisos; gate final .spec/audit-t092.json após documentos/tarefa/plano. Somente T-092 concluída, T-032 a T-037 pendentes. Ver .spec/revisao-t092.md para tentativas/limites/preservação.
