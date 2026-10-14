# Matriz de persistência de catálogo e orçamento — T-032

Escopo: US-020/US-021/AC-045. A existência de DbSets/configurações ou audit verde anterior não encerra esta revisão. Evidências novas ficam em `.local-verification/t032`; a certificação final deve conferir TRX, motor e `record.exitCode` separadamente.

| Requisito / cenário | Camada e prova executável | Alcance |
| --- | --- | --- |
| DbSets, PK UUID, identidade gerada, tenant required/tenant_id | ServiceCatalogMappingTests.EffectiveMetadataAndDiscoveredMigrations | Modelo Npgsql efetivo após todas convenções; sem conexão |
| Name200/Description1000 nullable/Unit50, BasePrice numeric(18,2), IsActive boolean required | Mesmo teste; ServiceCatalogPersistenceTests.PhysicalRequiredLengthsNumericAndTenantForeignKey | Metadata e constraints físicas são provas separadas |
| Índices tenant, tenant/ativo/nome, AK tenant/Id e FK tenant Restrict | Metadata acima; PostgreSqlRlsTests.EveryTenantTableHasRequiredDatabaseProtection | AK criada por HasPrincipalKey por convenção; catálogo físico/RLS inspecionado |
| Limites inclusivos, Unicode BMP/suplementar, null/empty descrição, preço zero/centavos/teto | ServiceCatalogValidationTests.AcceptsBoundaries / UnicodeScalarBoundaries; ServiceCatalogPersistenceTests.CompleteRoundtripAndUpdate | Contagem Rune corresponde a varchar; domínio e roundtrip real em contextos novos |
| Excesso de nome/descrição/unidade recusa antes de mutar | ServiceCatalogValidationTests.RejectsExcessWithoutMutation (unitário) | Todos campos/identidades preservados |
| Recusa create/update com válido enfileirado, sem SaveChanges indevido | ServiceCatalogValidationTests.HandlersRejectWithoutSaving | InMemory com guarda, contador, tracker e SaveChanges posterior grava apenas válido |
| HTTP limites e 400 seguro, update recusado não inativa nem muda conteúdo | ServiceCatalogValidationTests.HttpBoundariesAndRejection e PostgreSqlHttpBoundariesAndRejection | Hosts InMemory e PostgreSQL restrito exclusivo; request/contexto novo |
| Roundtrip integral e atualização/inativação sem trocar identidade | ServiceCatalogPersistenceTests.CompleteRoundtripAndUpdate | Todos campos, opcionais, Unicode, teto/zero/centavos e estados em contextos novos |
| Filtro catálogo A/B/sem tenant | ServiceCatalogMappingTests.FiltersRequireMatchingTenant | EF InMemory: IgnoreQueryFilters remove esta barreira deliberadamente |
| Guarda catálogo Added/Modified/Deleted, tenant original/current/ausente, batch válido+inválido | ServiceCatalogMappingTests.InvalidBatchCannotPersist (18 casos) | Sync/async; materialização nova confirma nenhuma gravação parcial; não é RLS |
| Guarda agregado orçamento Added/Modified/Deleted original/current/ausente, batch | QuotationAggregateGuardTests.RejectedAggregateWriteCannotPersistValidQueuedWrite (26 casos) | Sem itens/sinal para não mascarar guarda do root, sync/async, todos escalares preservados |
| Filtros orçamento/itens A/B/ausente | QuotationMappingTests.FiltersApplyToAggregateAndDirectItemQueries | Reuso pertinente T-089, renovado integralmente |
| Guarda itens/sinal owned/OS com owner inalterado | QuotationMappingTests; QuotationConversionIntegrityTests | Original/current, owner e shadows; reuso T-089/T-091, não substitui RLS |
| EF IgnoreQueryFilters e SQL sem tenant/entre tenants | ServiceCatalogPersistenceTests.EfAndIndependentRlsIsolation; QuotationPersistenceTests.AggregateAndStandaloneDependentsAreIsolated | Login restrito, RLS independente dos filtros e guardas |
| SQL SELECT/INSERT/UPDATE/DELETE com logins sem bypass/propriedade | PostgreSqlRlsTests.DirectSqlCannotCrossTenantBoundaries | Inclui ServiceItems, Quotations e dependentes entre as 16 tabelas; linha completa evita falso positivo NOT NULL |
| RLS ENABLE/FORCE, tenant NOT NULL/FK/índice/policies | PostgreSqlRlsTests.EveryTenantTableHasRequiredDatabaseProtection | Catálogo real em base temporária; não pressupõe rollout no banco de trabalho |
| NOT NULL, textos excessivos, overflow e FK tenant inexistente | ServiceCatalogPersistenceTests.PhysicalRequiredLengthsNumericAndTenantForeignKey | Owner temporário isola constraint sem RLS mascarar; savepoints e snapshots após cada falha |
| Cliente/orçamento, item/owner, catálogo nullable composto e Restrict/Cascade | QuotationMappingTests.EffectiveMappingAndSnapshotRemainConsistent; QuotationPersistenceTests.ReferentialIntegrityAndDeleteRules; QuotationConversionIntegrityTests.PhysicalConstraintsRejectInvalidWrites | Metadata simples e SQL histórico composto diferenciados; referências externas/inexistentes recusadas; item manual null e cascade real |
| Atualizar/inativar catálogo não muda valores/vínculos históricos orçamento e OS | ServiceCatalogPersistenceTests.InactivationPreservesHistoricalLinksAndRestrict | Snapshot JSON integral de agregados e itens dos dois tenants, contexts novos e Restrict de cada FK isoladamente |
| Orçamento/owned roundtrip completo, null/zero/estados | QuotationPersistenceTests.CompleteAggregateRoundtrip | Reuso T-089; novos contextos, não tracker que gravou |
| Descoberta/ordem das 16 migrations, designers/snapshot, sem mudança pendente | ServiceCatalogMappingTests.EffectiveMetadataAndDiscoveredMigrations; QuotationConversionIntegrityTests.MappingMatchesSnapshot | Lista exata e HasPendingModelChanges=false; AddRowLevelSecurity histórico não descoberto |
| Catálogo histórico completo preservado no upgrade RLS→final | ServiceCatalogPersistenceTests.HistoricalCatalogUpgradePreservesAllRows | JSON integral de ServiceItems ambos tenants, null/empty/zero/centavos/teto/ativo-inativo; snapshot final e materialização restrita |
| Upgrade catálogo/orçamento válido e externo/órfão recusado | QuotationPersistenceTests.CatalogMigrationPreservesValidHistoricalData / CatalogMigrationRejectsInvalidHistoricalData | Dados/history/constraint após falha, sem limpeza automática; reuso T-089 |
| Upgrade OS válido e duplicado/externo/inexistente recusado | QuotationConversionIntegrityTests.UpgradePreservesValidHistory / UpgradeRejectsInvalidHistoryAtomically | Linhas/schema/history revertidos transacionalmente; reuso T-091 |
| Dinheiro Q-020/Q-021/RequiredAmount e criação pública Pending | QuotationModelValidationTests/QuotationModelHttpTests/QuotationIntegrationWorkflowTests | Regressões T-031/T-092; SQL numeric arredonda entradas de escala excessiva, validação de negócio antecede gravação |

## Diferenças deliberadas e limites

FKs compostas cliente/owner `FK_rls_*` e índices `UX_rls_*` foram instalados por SQL histórico. Modelo/snapshot ainda descrevem FKs simples desses vínculos. Catálogo composto nullable Restrict e AK tenant/Id estão no modelo efetivo/designer/snapshot; não criar migration cosmética para apagar constraints físicas existentes. Corpo do snapshot final acompanha o último designer. Não há mudança de schema necessária para validar textos contra limites já existentes.

As provas novas não certificam implementação completa de T-033 a T-037, UI catálogo, PDF integral ou entrega real de mensagens. Draft interno/Pending público, edição Pending, decisões idempotentes, conversão Scheduled e contratos distintos das duas rotas permanecem regressões existentes. JSON number/JavaScript tem limitação preexistente nos extremos; limites decimais são provados em .NET/PostgreSQL. Contexto manual stale/transação externa não são certificados; rollback relacional não restaura ChangeTracker.

## Seis skips históricos

Preservados e sem valor probatório: UserPersistenceTests (1), WorkOrderApiTests (1), RowLevelSecurityTests (1), MigrationSafetyTests (1), PostgreSqlFixtureTests (2). A matriz T-092 identifica seus arquivos/escopo; certificação de RLS/migrations/conversão vem das provas posteriores executadas, nunca desses skips.

## Resultado final

15features aceitas em `.local-verification/t032/certified-complete`, motor=0 e record.exitCode=0 em todas. Cada execução476resultados:149unitPASS,290integraçãoPASS+6skips históricos,31arquiteturaPASS =470PASS/zero falhas.62casos novos efetivamente executados (6unit/56integração). Índice `.local-verification/t032/final-certified-summary.json`;112observações PostgreSQL novas preservadas, incluindo tentativa diagnóstica, com alcance identificado no índice. Audit de revisão --ci exit0,95/95critérios provados, zero erros/avisos; gate final `.spec/audit-t032.json`. Somente T-032 concluída; T-033 a T-037 continuam pendentes. Ver revisão para falhas intermediárias e cleanup próprio comprovado.
