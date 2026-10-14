# Design — Catálogo de serviços e modelo de orçamentos (T-031)

## Revisão e escopo
Não existia design.md desta feature na árvore nem no histórico consultado. Este documento registra a revisão do modelo atual; não certifica tarefas históricas de persistência, endpoints, PDF ou estados.
ServiceItem e Quotation implementam ITenantOwned. Factories recusam tenant vazio; autenticação, filtros EF e RLS continuam responsáveis pelo pertencimento em runtime. Criação e edição conferem cliente e referências ao catálogo no tenant autenticado.
Quotation nasce Draft internamente; CreateQuotationHandler valida conteúdo completo e publica Pending. Isso preserva AC-090 sem remover a montagem do agregado.

## Decisões confirmadas pelo usuário em 12/10/2026
Valores monetários de entrada (preço base/unitário, desconto, sinal fixo), percentual e quantidade aceitam até duas casas decimais. Quantidade deve ser positiva. Valores e agregados monetários e quantidade têm teto 9.999.999.999.999.999,99. Não há normalização silenciosa de entradas com precisão excessiva.
Subtotal = preço unitário × quantidade, arredondado por item para duas casas, MidpointRounding.AwayFromZero. Desconto absoluto incide sobre esse subtotal; pode ser igual a ele, nunca maior. Total geral = soma dos subtotais arredondados − soma dos descontos. Subtotal, desconto e total agregados precisam caber no teto mesmo quando o desconto reduziria o total final.
Entrada percentual = total × percentual / 100, arredondada para duas casas pelo mesmo método; percentual entre zero e 100. Entrada fixa entre zero e total. Saldo = total − entrada já arredondada. Zero e itens gratuitos são válidos. Enum desconhecido é recusado.
Quantidade também fica limitada à precisão já persistida pela OS, sem migration. O consumidor de OS usa o mesmo subtotal arredondado. Recebíveis usam RequiredAmount calculado pelo orçamento, impedindo divergência entre entrada/saldo e valores persistidos.

## Atomicidade e compatibilidade
AddItem/RemoveItem calculam uma lista candidata e validam totais e sinal antes de substituir itens ou campos. Edição pendente mantém o agregado substituto validado antes de qualquer mutação. Validações financeiras usam ArgumentException, mantendo respostas 400 ProblemDetails já documentadas; DTOs, rotas e enums numéricos não mudam.
Não alterar guardas de estados, idempotência financeira, locks, destinatários/condições das notificações, opt-out ou renderização serial do PDF.
Frontend deve aplicar arredondamento por item também na prévia e comparar desconto contra subtotal arredondado; a API permanece autoridade final. JSON number e consumidores JavaScript têm precisão limitada para extremos, uma restrição preexistente do contrato; prova dos extremos do domínio/persistência usa decimal .NET, não certifica exatidão de centavos em JavaScript acima da sua precisão numérica.

## Sobreposição
T-088 compartilha Quotation, itens, entrada e enums, mas abrange estados e critérios próprios. Permanece pendente; T-031 não certifica T-032 a T-037 nem T-088 a T-092 automaticamente. A revisão não altera a política histórica Cancel de Expired para Canceled.

## Provas
QuotationModelValidationTests: limites, arredondamento, entradas inválidas, tenant e ausência de mutação nos métodos de domínio.
QuotationModelHttpTests: recusa HTTP sem persistência/mutação, catálogo externo/inexistente, roundtrip e recebíveis sob PostgreSQL temporário exclusivo com logins restritos/RLS.
Regressões existentes: edição/concorrência, permissões, CPF/PreserveDocument, isolamento, financeira/notificações e PDF semântico concorrente. Logs/TRX/JSON por execução em .local-verification/t031; confirmar motor e record.exitCode separadamente.

## Revisão T-088 — ciclo de vida

O agregado monta Draft, mas a API publica apenas Pending após validar referências e conteúdo. Itens são expostos por coleção somente de leitura. TenantId permanece setter público por ITenantOwned/EF; a guarda de gravação compara tenant original e atual, filtros EF e RLS protegem leitura/persistência. A factory sozinha não certifica isolamento.

Transições de domínio e operações de montagem serão provadas por matriz de estados e snapshots completos. Expiração usa DateTime.UtcNow.Date, considera validade de hoje ainda válida e só muda Pending. Cancel de Expired para Canceled e repetição Canceled permanecem históricos. Estados Approved/Rejected/Converted bloqueiam alterações de conteúdo e decisões no domínio.

Uma recusa no handler de status não deve executar CheckExpiration antecipadamente: isso modificava o objeto rastreado embora não salvasse no banco. Validação de vencimento agora precede a decisão, sem mutação; cancelamento de vencido continua permitido. Não há mudança de DTO/rota/status HTTP/OpenAPI ou migration.

Aprovação pública repetida é uma exceção idempotente implementada no handler para Approved/Converted. O domínio continua estrito. O usuário resolveu Q-022 em 12/10/2026: AC-093 admite aprovação pública repetida idempotente de Approved/Converted sem mutação/duplicação e mantém rejeições repetidas bloqueadas. Conversão e suas proteções permanecem existentes, sem certificação integral de T-091 por esta revisão de domínio.

## Revisão T-089 — mapeamento efetivo e integridade

Quotation tem PK UUID, cliente e tenant obrigatórios, Code varchar(50), Notes/PaymentTerms varchar(2000), status integer com enum estável, IssueDate/ValidUntil timestamptz (validade opcional), totais numeric(18,2). Índices tenant/código único, tenant/status e tenant/cliente permanecem. Customer Restrict e Items Cascade preservados; navegação Items usa explicitamente o campo _items. QuotationItem tem PK UUID, QuotationId/tenant_id obrigatórios, referência de catálogo nullable, nome/unidade obrigatórios e valores numeric sem typmod, como no schema histórico. As regras de entrada Q-020/Q-021 continuam no domínio; esta revisão não afirma que SQL bruto valida dinheiro ou enum.

DepositInfo é owned opcional na mesma tabela de Quotation; sua ausência deixa as colunas físicas null, presença materializa inclusive zeros/IsPaid=false. Value/RequiredAmount/RemainingBalance são numeric(18,2), notas varchar(1000), Type integer e IsPaid boolean. Não há tabela separada nem policy distinta: a linha do orçamento é a barreira RLS.

O filtro EF explícito de QuotationItem usa o TenantId shadow e contexto autenticado. A guarda valida original/current nos itens Modified/Deleted e recusa Added com tenant externo ou contexto ausente antes de preencher shadows. Escritas de DepositInfo validam o owner Quotation rastreado, seu tenant original/current, mesmo quando o owner está Unchanged; owned sem owner é recusado. RLS permanece proteção independente, incluindo IgnoreQueryFilters/SQL direto.

A migration histórica RLS acrescenta FKs compostas cliente/orçamento e item/orçamento por SQL, além das FKs simples descritas pelo modelo/snapshot. A nova EnforceQuotationCatalogTenantReference corrige uma lacuna real: FK composta (tenant_id, ServiceItemId) para ServiceItems(tenant_id, Id), chave alternativa e índice correspondente, Restrict. Itens manuais com ServiceItemId=null continuam válidos. Catálogo referenciado não pode ser excluído; fluxos existentes usam desativação. Referências históricas inválidas impedem upgrade transacional; nenhuma linha é limpa automaticamente. A migration será aplicada somente a bases temporárias nesta tarefa.

T-032 sobrepõe DbContext/configuração, mas inclui persistência e migrations do catálogo como entrega própria; não é encerrada por esta revisão de orçamentos. T-090 a T-092/T-033 a T-037 continuam pendentes. Nenhum contrato HTTP/OpenAPI muda, frontend/schema não precisam regeneração. Concorrência integral de conversão permanece fora da certificação T-089.

## Revisão T-090 — endpoints e contratos observados

Program/DI integram as sete operações de QuotationEndpoints. RequireAuthorization e RequirePermission fazem autenticação/permissão por operação; contexto do tenant é obtido da identidade ativa, sem campo de tenant nos comandos. Detalhe usa DTO explícito e não retorna documento do cliente. GET usa filtros EF/RLS e total filtrado, com validação Page/PageSize/status antes da consulta. Offset calculado em long evita overflow; máximo100 limita volume. ThenBy(Id) desempata datas, sem garantia de snapshot entre páginas sob gravações concorrentes.

QuotationInputValidation compartilhada por criação/edição valida limites de texto já existentes no schema e UTC para timestamptz, antes de qualquer mutação. Verificação de código existente usa filtro do tenant; edição exclui o próprio ID. Endpoint trata somente UniqueViolation da constraint IX_Quotations_tenant_id_Code como 400 seguro para corrida posterior ao precheck, preservando 500 seguro para falhas inesperadas. Banco mantém atomicidade da gravação; edição já usa transação/lock T-103. Nenhuma migration necessária.

Metadata descreve limites, datas, ordem e duplicação; POST documenta Location201. Documento é exportado pelo host InMemory e schema frontend regenerado pelo arquivo; consumidores usam UTC/toISOString e páginas de tamanho10. Provas próprias HTTP/InMemory/PostgreSQL restrito, rastreamento/SaveChanges posterior e corrida de criação complementam ciclo de vida, cálculos e edição existentes. T-034/T-036 compartilham handlers/rotas mas têm escopos próprios; T-091/T-092 e T-032 a T-037 não são concluídas automaticamente. Concorrência integral da conversão segue fora desta certificação.

## Conversão em OS — revisão T-091 — 14/10/2026

As duas rotas já são registradas por Program/DI; referências históricas a handler órfão não são diagnóstico atual. POST /api/v1/quotations/{id}/convert conserva 200 JSON {workOrderId}; POST /api/v1/work-orders/from-quotation/{quotationId} conserva 201 JSON {id} e Location para a OS, inclusive na repetição. Ambas exigem autenticação/sales.write, obtêm tenant exclusivamente da identidade e não recebem body: conteúdo/mídia enviados não representam comandos nem tenant. UUID malformado não casa rota e retorna404 vazio; vazio/inexistente/externo retorna400 seguro indistinguível dentro de cada rota. As mensagens entre as duas rotas permanecem históricas e distintas.

Somente Approved produz uma nova OS. Repetição retorna o mesmo ID sem modificar OS, orçamento ou recebíveis. Converted sem OS não cria outra por recuperação implícita. A nova OS tem Scheduled, CreatedAt UTC e CompletedAt null: aguarda início manual, sem iniciar execução nem criar agendamento. Código recebe OS-, cliente/tenant/orçamento são vinculados, notas e todos os campos dos itens são copiados com identidades próprias. Termos, validade, emissão e sinal permanecem no orçamento vinculado; recebíveis originados na aprovação não são recalculados pela conversão.

Conversão, edição e decisões compartilham lock da linha Quotations no PostgreSQL. A conversão abre transação, adquire FOR UPDATE antes de consultar OS/orçamento, valida estado, monta OS, muda o orçamento e salva/commita conjuntamente. Replay consulta a OS após o lock. RLS e condição explícita do tenant protegem o lock. O índice único (tenant_id,QuotationId) é a barreira física adicional. Recusas de validação precedem staging/mutação; falha relacional reverte todas as linhas e exige descarte do scope HTTP, pois rollback não restaura ChangeTracker. O handler é transient, com DbContext/UnitOfWork scoped pela DI; cada requisição usa seu contexto, sem reutilizar agregados carregados antes de uma transação externa.

A migration 20261013202849_EnforceWorkOrderConversionIntegrity amplia Code da OS50→53 e Notes1000→2000 e nomes/unidades dos itens para text, preservando orçamentos públicos válidos sem truncar dados. Acrescenta unicidade por tenant/orçamento e FK composta nullable item/catálogo com Restrict. FKs compostas históricas OS/cliente/orçamento e item/OS, filtros EF dos owners, RLS e guarda de escrita original/current são preservados; guarda owned nova valida owner/tenant inclusive owner Unchanged. SQL bruto continua sem CHECK financeiro/estado; domínio mantém Q-020/Q-021, subtotal arredondado e desconto limitado. Migração inválida por duplicidade ou catálogo externo/inexistente deve recusar upgrade atomicamente, sem limpeza de legado.

Esta revisão é exclusiva de T-091. T-092 tem revisão própria de integração geral; T-034 abrange orquestração/cálculos/status; T-036 inclui catálogo/PDF; T-037 inclui integração geral/PDF e T-103 mantém edição Pending. Nenhuma delas é concluída automaticamente. Migration validada apenas em bases temporárias exclusivas; não aplicar ao banco de trabalho ou iniciar esta API nele sem rollout autorizado.

## Fechamento T-091 — 14/10/2026

Somente T-091 concluída após revisão própria, 39 casos novos e certificação completa. As 15 features renovadas têm motor=0 e record.exitCode=0, cada uma com406 resultados (400PASS, zero falhas e seis skips antigos). Logs/TRX/JSON preservados em .local-verification/t091/certified-complete; índice final em final-certified-summary.json. Audit --ci exit0, ok=true,95/95 critérios provados, zero erros/avisos (.spec/audit-t091.json).

Frontend: schema regenerado pelo OpenAPI local após documentar Location201 da rota alternativa; documentação instalada Next.js16.2.11 consultada. Typecheckexit0 e232/232 testes PASS, sem alteração de componentes/BFF. Exportação OpenAPI hostInMemory exit0/1PASS; contrato observado200/workOrderId e201/id/Location preservado, inclusive replay.

T-092 e T-032 a T-037 permanecem pendentes; T-031/T-088/T-089/T-090/T-103 mantêm conclusão. Plano sequencial para sete tarefas regenerado somente como artefato; executor não iniciado e modelos históricos não adotados. Sem commit/push/deploy, serviço5050, entrega real WhatsApp/e-mail ou telas financeiro/equipe. Nova migration não aplicada ao banco de trabalho; quatro migrations continuam exigindo rollout explícito. Ver .spec/revisao-t091.md e docs/POSTGRESQL_RLS.md.

## Integração e validade das fixtures — T-092 — 14/10/2026

QuotationTests deixou de depender de IntegrationTestDb compartilhado e de UoW resolvido de outro scope. TeamFinancialTestFactory mantém raiz InMemory reutilizada para evitar ManyServiceProviders, nome de banco exclusivo por host e TenantSaveChangesInterceptor tanto na preparação quanto no contexto do handler. UnitOfWork é construído com o mesmo DbContext; leitura posterior usa outra instância. Nenhum warning, filtro ou asserção foi suprimido.

A matriz .spec/matriz-t092.md separa domínio puro, handlers diretos, EF InMemory, HTTP e PostgreSQL restrito/RLS físico. Os seis novos casos de decisões finais carregam o grafo em contexto novo, capturam todos valores escalares e exigem identidades/quantidade rastreadas preservadas, EntityState.Unchanged e SaveChanges0. A jornada HTTP composta em ambos provedores completa a revisão própria sem duplicar matrizes extensas anteriores.

O contrato de produção usa DbContext/UoW scoped e handlers transient. Cada request inicia com tracker novo. Um agregado previamente materializado em contexto manual pode continuar stale mesmo após um lock, pois consulta tracking reutiliza instâncias; isso não representa o contrato HTTP certificado. UnitOfWork.BeginTransactionAsync inicia sua própria transação, sem promessa de participação em transação externa existente. Não ampliar esses contratos nesta tarefa de testes; descartar scope após erro relacional e manter esses limites explícitos.

Sem mudança de código de produção, DTO, rota, OpenAPI, frontend ou migrations; não há regeneração de schema necessária. Bases temporárias dos testes continuam exclusivas; banco/cadastros/credenciais de trabalho e quatro migrations ainda pendentes de rollout permanecem preservados. T-032 a T-037 continuam pendentes.

## Fechamento T-092 — 14/10/2026

Somente T-092 concluída após revisão própria das fixtures/cobertura conjunta, matriz requisito/cenário/prova, oito casos novos e ampliação das asserções antigas. As15features renovadas têm motor=0/record.exitCode=0,414 resultados por execução:408PASS, zero falhas e seis skips antigos. Provas completas em .local-verification/t092/certified e final-certified-summary.json; matriz .spec/matriz-t092.md, revisão .spec/revisao-t092.md e gate final .spec/audit-t092.json. Audit de revisão exit0/95 critérios provados/zero erros e avisos.

T-032 a T-037 permanecem pendentes de revisão própria; plano sequencial das seis tarefas regenerado somente como artefato, sem executar headless/modelos históricos. Nenhum contrato/produção/OpenAPI/frontend/migration alterado. Preservados cinco obj,2.080 evidências anteriores por hash, schema frontend e backup; sem commit/push/deploy, API5050 ou rollout no banco de trabalho.

## Persistência de catálogo e migrations — revisão T-032

DbSets e configurações já existem. O modelo Npgsql efetivo, depois das convenções de tenancy, usa ServiceItems com PK UUID/ValueGenerated.OnAdd (factory atribui Guid), tenant_id obrigatório/FK Restrict/índice tenant, Name varchar(200) obrigatório, Description varchar(1000) nullable, Unit varchar(50) obrigatório, BasePrice numeric(18,2) e IsActive boolean obrigatórios. Índice tenant/IsActive/Name e chave alternativa tenant/Id criados pelas referências compostas permanecem. Filtro exige HasTenant e igualdade do tenant autenticado.

Lacuna corrigida minimamente: Create/Update do domínio recusam comprimento acima de 200/1000/50 antes de atribuir qualquer campo. Limites contam caracteres Unicode escalares (Rune), como varchar PostgreSQL, inclusive símbolos suplementares. Não truncam/normalizam textos; descrição null/empty e preço zero/centavos/teto confirmado continuam válidos. ArgumentException mantém o 400 ProblemDetails público seguro existente, sem alterar DTOs/rotas/OpenAPI. Handler recusado não chama SaveChanges e não inativa; um SaveChanges explícito posterior grava apenas a operação válida previamente enfileirada, preservando o original.

Migrations descobertas são 16 em ordem cronológica, de InitialTenant até EnforceWorkOrderConversionIntegrity. AddRowLevelSecurity histórico não tem descoberta ativa. Snapshot final acompanha o último designer, HasPendingModelChanges=false. FKs compostas cliente/owner e índices FK_rls_*/UX_rls_* instalados por SQL histórico complementam as FKs simples do snapshot deliberadamente. Não gerar migration para eliminar essa diferença nem recriar infraestrutura existente.

Matriz .spec/matriz-t032.md registra metadata, domínio/handlers, InMemory, HTTP e PostgreSQL restrito separadamente. Roundtrip e update/inativação conferem novos contextos; snapshots integrais demonstram que valores/identidades/vínculos dos itens de orçamento/OS permanecem. Upgrades históricos válidos preservam catálogo completo e inválidos externos/órfãos/duplicados recusam com rollback de dados/schema/history, sem limpeza automática. Reuso pertinente dos testes T-089/T-091 é renovado, sem duplicação artificial.

SQL numeric(18,2) valida overflow e arredonda escala excessiva segundo PostgreSQL; regras Q-020/Q-021 de rejeição antecipada permanecem no domínio. Owner temporário isola provas NOT NULL/length/FK, login restrito comprova RLS independente (inclusive IgnoreQueryFilters). Nenhuma migration nova ou rollout no banco de trabalho; T-033 a T-037 não são certificadas por esta revisão.

## Fechamento da persistência — T-032

Revisão própria concluída com62casos novos e15features aceitas motor0/record0,476resultados/470PASS/zero falhas/6skips por execução. Audit de revisão --ci exit0/95critérios/zero erros e avisos; gate final .spec/audit-t032.json. Evidências completas em .local-verification/t032/certified-complete, índice final-certified-summary.json, matriz .spec/matriz-t032.md e revisão .spec/revisao-t032.md. Somente T-032 concluída; T-033 a T-037 pendentes. Dados/credenciais/schema de trabalho, processos do usuário, cinco obj e evidências anteriores preservados; sem commit/push/deploy ou startup API5050.
