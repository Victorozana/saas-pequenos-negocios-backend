# Revisão própria T-032 — persistência EF Core e migrations

Execução manual em 14/10/2026, backend main/17eca5b e frontend master/1733b8c, ambos com upstream sincronizado ao início. Modelo/esforço atuais mantidos; três auxiliares herdados usados conforme autorização explícita do prompt. Sem headless, commit/push/deploy ou startup API5050. Cinco obj preexistentes copiados e hashados antes dos builds; schema, checks, dump e evidências locais preservados.

## Leitura e fronteira

Lidos AGENTS dos dois repositórios, skill local onp-spec-driven, continuidade integral, POSTGRESQL_RLS, constituição, spec/design/tasks/plano e revisões/audits T-023/T-031/T-088/T-089/T-090/T-091/T-092/matriz T-092. Verificado TRX T-092 real: 53 resultados QuotationMapping/QuotationPersistence/QuotationConversionIntegrity Passed, integração234PASS+6skips, record0. Reuso pertinente não encerra catálogo: T-032 exige metadata/roundtrip/guardas/limites/upgrades próprios. Comparação com T-103 mantém edição Pending; T-033 a T-037 permanecem pendentes.

## Achados e mudança

DbSets ServiceItems/Quotations e configurações já existem. Metadata Npgsql final e convenções de tenancy preservam chaves, obrigatoriedade, comprimentos, numeric(18,2), índices, filtros, relacionamento tenant e catálogo composto nullable Restrict. Nenhuma lacuna de schema exige migration. Designers/snapshot final coerentes, 16 migrations descobertas em ordem e HasPendingModelChanges=false; AddRowLevelSecurity histórico sem descoberta ativa. FKs compostas cliente/owner FK_rls_* e índices UX_rls_* permanecem SQL histórico deliberado, além das FKs simples no modelo/snapshot.

Lacuna real corrigida: ServiceItem.Create/Update aceitavam Name>200, Description>1000 e Unit>50, falhando somente na persistência relacional. Agora validam antes de qualquer atribuição/inativação/staging/SaveChanges. Comprimento conta caracteres Unicode escalares (Rune), alinhado a varchar PostgreSQL, preservando emojis/símbolos suplementares legítimos. Não há truncamento; null/empty descrição, preço zero/centavos/teto Q-020 continuam. DTOs/rotas/OpenAPI/schema não mudam; 400 ProblemDetails seguro existente comprovado HTTP InMemory e PostgreSQL restrito. Frontend não foi modificado/testado novamente nesta tarefa.

## Provas próprias e reuso

62 casos novos: seis unitários, 56 integração. ServiceCatalogMappingTests: modelo efetivo/cadeia/filtros e18casos guarda sync/async Added/Modified/Deleted tenant ausente/externo/original/current, batch válido bloqueado e contexto novo. QuotationAggregateGuardTests:26casos do root sem itens/sinal mascarando guarda, snapshots todos escalares em novo contexto. ServiceCatalogValidationTests:3casos handlers com contador SaveChanges/tracker, save posterior só do válido enfileirado, dois hosts HTTP com limites/400 seguro e materialização nova.

ServiceCatalogPersistenceTests:5provas PostgreSQL exclusivo/restrito de roundtrip integral (null/empty/Unicode/teto/zero/centavos/ativo-inativo/update), RLS independente IgnoreQueryFilters/SQL sem tenant, constraints NOT NULL/length/numeric/FK isoladas por owner temporário/savepoints, snapshots completos orçamento/OS/itens preservados após update/inativar e Restrict de cada FK isoladamente, catálogo histórico completo preservado RLS→final com novo contexto/snapshot consistente.

Matriz .spec/matriz-t032.md diferencia metadata, domínio/handlers, InMemory, HTTP e PostgreSQL físico. Reuso renovado: T-089 orçamento/itens/sinal/filtros/guardas/cliente/owner/FKs/Cascade e upgrade externo/órfão; T-091 OS/catalogo nullable/Restrict/unicidade e upgrade duplicado/externo/inexistente com rollback linhas/schema/history. PostgreSqlRlsTests prova policies ENABLE/FORCE/grants e SELECT/INSERT/UPDATE/DELETE em todas16tabelas. Não duplicar artificialmente testes completos atuais.

## Tentativas preservadas

- focused-01:exit1,27PASS/2FAIL. Teste novo contava17 migrations incluindo fonte histórica não descoberta; corrigido para lista exata16. Primeira preparação de upgrade alvo préRLS não permitia fixture ler tenant_id; início corrigido para RLS, etapa pertinente anterior às migrations catálogo/OS. TRX preservado.
- focused-02:exit0,29PASS/zero falhas/skips. Posteriores complementos HTTP PostgreSQL/guarda root/Unicode/snapshots amplos exigem certificação nova, não se atribuem a essa execução.
- certified/cadastro-tenants: runner integração não compilou por Database ambíguo. Unit148PASS/arquitetura31PASS,179resultados; motor0/record1 recusado pelo coletor. Corrigido nome qualificado, nenhum teste removido/enfraquecido.
- certified-final/cadastro-tenants:475resultados,468PASS/1FAIL/6skips, motor0/record1 recusado. Falha Npgsql timeout em Database.DisposeAsync linha267 (DROP DATABASE temporária), não nas asserções SQL/RLS; prova ainda é FAIL. Consulta diagnóstica inicial usou prefixo rls_* incompleto: não certificou limpeza das bases agendamento_rls_*. Corrigida a consulta na repetição; confirmação de recursos remanescentes exige checagem final. Sinal VERIFY_FALHOU::cadastro-tenants::P-004 acrescentado preservando todo histórico. Versão compilada antecedeu Unicode/snapshot ampliado, não certifica essas mudanças.
- certified-complete:15/15features motor0/record0,476resultados por execução:470PASS/zero falhas/6skips.149unitPASS,290integraçãoPASS+6skips,31arquiteturaPASS. Todos62casos novos executados. Logs/TRX/JSON e índice final-certified-summary.json preservados;112observações PostgreSQL novas copiadas para t032/postgresql-observations, incluindo a tentativa diagnóstica, sem apagar observações antigas.
- Conferência final: primeiro coletor encapsulou o array ConvertFrom-Json em PowerShell5; corrigida atribuição direta, mantendo validação das15features. Outra tentativa recusou trailing whitespace emitido pelo gerador HTML nos fragmentos vazios. Corrigidas três interpolações em `.agents/skills/onp-spec-driven/scripts/lib/src/core/plano.js`, sem mudar verify/audit/gate nem editar artefato gerado à mão; node --check exit0 e plano regenerado. Logs diagnósticos preservados; conferência final passou exit0 com todas as asserções mantidas.

## Cleanup da tentativa falha e preservação

A consulta com prefixo correto identificou duas bases sem sessões: uma preexistente de12/10/2026, preservada; outra criada em14/10/2026 às18:26:57.2223964UTC,11ms após início do teste RLS falho registrado no TRX (18:26:57.2110207UTC), dentro do intervalo da falha. Catálogo registra CREATE DATABASE na transação261674 e os dois logins da sequência da fixture nas transações261761/261763. Nomes exatos, origem, ausência de sessões e atributos restritos foram checados antes de qualquer cleanup.

cleanup-owned-resources.ps1 removeu somente essa base e esses dois logins próprios, sem FORCE/terminação de processos do usuário; exit0. Proveniência e resultado em owned-cleanup-provenance.json / owned-cleanup-result.json. A base temporária anterior e todas credenciais/bancos de trabalho foram preservados; nenhuma limpeza ampla por prefixo. Não apresentar a consulta inicial incorreta como prova de ausência global de recursos.

Conferência final própria passou exit0: restaurou os cinco obj exatamente por hash, conferiu338arquivos de fonte/testes estáveis e2.657entradas de evidência/schema/dump/checks anteriores. Sinais antigos preservados integralmente, com uma adição legítima VERIFY_FALHOU da tentativa recusada; não restaurar sinais apagando essa ocorrência. HEADs/branches esperados permanecem, diff --check dos dois repositórios exit0 e zero listeners5050, conforme final-environment.json. final-postgresql-resources.json confirma somente uma base temporária preexistente preservada, zero logins temporários e zero sessões temporárias remanescentes. Nenhuma lição nova adicionada; sugestões históricas conservadas no log.

## Limites operacionais e de certificação

SQL numeric(18,2) arredonda escala excessiva; regra de rejeitar precisão excessiva é domínio antes da persistência. Sem CHECK SQL novo financeiro/estado. JSON number/JavaScript continua impreciso nos extremos; UI catálogo histórica preço positivo não certificada aqui. HTTP tem contextos novos por request; contexto manual stale/transação externa fora do contrato certificado; rollback relacional não restaura ChangeTracker. Seis skips antigos identificados na matriz T-092 e preservados não são prova.

Quatro migrations posteriores continuam NÃO aplicadas a agendamento_local: AddCustomerAutomaticReminderPreference, AddDailyReceivableReminderDeduplication, EnforceQuotationCatalogTenantReference e EnforceWorkOrderConversionIntegrity. RLS/histórico notificações já tiveram rollout local histórico; não confundir notas antigas com estado atual. Não alterado banco/cadastros/credenciais de trabalho; dump runtime-repair preservado. Sem entrega real WhatsApp/e-mail, UI financeiro/equipe ou implementação das entregas T-033 a T-037.

## Resultado e gate

Audit de revisão `.local-verification/t032/audit-review.json`:exit0,ok=true,15features,95/95critérios provados, zero erros/avisos; uma suposição global aberta preexistente e zero perguntas abertas. Somente T-032 concluída após revisão própria e todas15provas aceitas. T-031/T-088/T-089/T-090/T-091/T-092/T-103 preservadas; T-033 a T-037 permanecem pendentes. Plano sequencial das cinco tarefas restantes regenerado somente como artefato; executor headless/modelos históricos não adotados.

Gate final JSON após documentos/plano/conferência: `.spec/audit-t032.json`, audit --ci exit0,ok=true,95/95critérios provados, zero erros/avisos. Saída textual final preservada em `.local-verification/t032/audit-final.log` e exit em audit-final-exit.txt. Próxima prioridade T-033, exigindo revisão própria dos handlers do catálogo; não encerrada pela cobertura incidental desta tarefa. Sem commit/push/deploy ou rollout/startup no banco de trabalho.
