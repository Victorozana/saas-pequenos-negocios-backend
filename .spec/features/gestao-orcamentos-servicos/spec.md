# Feature Spec: Gestão de Orçamentos e Serviços

> feature: gestao-orcamentos-servicos
> status: em-implementacao

## Visão Geral
Esta feature permite ao prestador de serviços criar orçamentos para seus clientes, adicionar serviços ao orçamento e aprovar ou rejeitar o orçamento, transformando-o posteriormente em ordem de serviço.

## User Stories (US)

### US-045 — Criar Orçamento para um Cliente
Como prestador de serviços,
Quero criar um orçamento associado a um cliente contendo os serviços prestados e seus valores,
Para enviar uma estimativa de custos formalizada.

#### AC-090 — Status inicial e soma dos serviços

- **Dado** um cliente e serviços com valores
- **Quando** o prestador cria um orçamento
- **Então** o status inicial é `Pendente` e o total é a soma dos valores dos serviços incluídos.

#### AC-092 — Orçamentos são isolados entre empresas

- **Dado** um orçamento pertencente a outra empresa
- **Quando** um usuário tenta acessá-lo ou alterá-lo
- **Então** o acesso e a alteração são negados.

O isolamento inclui consultas diretas de QuotationItem, alterações/exclusões de itens e alterações apenas no sinal owned. Filtros EF e guarda de gravação complementam RLS; remover filtros EF não remove a proteção PostgreSQL. Cliente é obrigatório e catálogo é opcional para itens avulsos; referências existentes devem pertencer ao mesmo tenant, incluindo integridade referencial física.

#### AC-206 — Edição permitida somente enquanto pendente

- **Dado** um orçamento já enviado ao cliente
- **Quando** o prestador tenta editar seu conteúdo
- **Então** a edição é permitida enquanto pendente e bloqueada após aprovação, rejeição ou conversão, sem modificar o registro bloqueado.

### US-046 — Aprovar ou Rejeitar Orçamento
Como prestador de serviços,
Quero poder alterar o status de um orçamento para Aprovado ou Rejeitado,
Para ter o histórico do funil de vendas.

#### AC-093 — Estados finais bloqueiam novas decisões com exceção da aprovação idempotente

- **Dado** um orçamento convertido em OS ou já aprovado/rejeitado
- **Quando** o prestador tenta aprová-lo ou rejeitá-lo novamente
- **Então** novas decisões são rejeitadas, exceto a aprovação pública repetida de orçamento `Aprovado` ou `Convertido`, que retorna sucesso idempotente sem alterar o orçamento nem duplicar recebíveis ou OS. Rejeições repetidas permanecem bloqueadas; no domínio as decisões repetidas continuam recusadas.

### US-047 — Transformar Orçamento Aprovado em Ordem de Serviço
Como prestador de serviços,
Quero gerar uma Ordem de Serviço (Work Order) a partir de um orçamento aprovado,
Para iniciar a execução dos serviços sem retrabalho de digitação.

#### AC-091 — Conversão exige aprovação

- **Dado** um orçamento do tenant autenticado
- **Quando** o prestador solicita conversão em OS
- **Então** somente um orçamento com status `Aprovado` pode ser convertido.

## Suposições

| ID | Suposição | Status | Resolução |
| --- | --- | --- | --- |
| ASM-016 | A conversão copia os serviços e cria a OS; não inicia execução automaticamente. | confirmada | Usuário confirmou início manual em 12/10/2026. |

## Perguntas em aberto

| ID | Pergunta | Status | Resposta |
| --- | --- | --- | --- |
| Q-016 | O orçamento pode ser editado depois de enviado para o cliente? | respondida | Usuário permite edição enquanto pendente; bloqueia após aprovação, rejeição ou conversão (12/10/2026). |
| Q-020 | Qual precisão, arredondamento e teto monetário devem ser usados? | respondida | Em 12/10/2026, usuário confirmou entradas até duas casas, subtotal por item e entrada percentual arredondados com meio para longe de zero, teto 9.999.999.999.999.999,99. |
| Q-021 | Como preservar quantidade na conversão para OS com numeric(18,2)? | respondida | Em 12/10/2026, usuário confirmou quantidade positiva com até duas casas e teto 9.999.999.999.999.999,99, rejeitada antes de criação/edição, sem migration. |
| Q-022 | AC-093 deve admitir aprovação repetida idempotente de Approved/Converted no fluxo público? | respondida | Em 12/10/2026, usuário confirmou documentar a exceção idempotente para aprovação pública repetida, mantendo rejeições repetidas bloqueadas. Domínio estrito e fluxo financeiro existente preservados. |

## Requisitos recuperados do histórico

Origem: commit f326f63. Os requisitos e tarefas antigos permanecem junto aos atuais; sua recuperação não certifica implementação nem RLS.

### US-020 — Cadastrar e Gerenciar Catálogo de Serviços/Produtos
Como prestador de serviço autenticado,
Quero cadastrar e gerenciar meus serviços/produtos com nome, descrição, unidade de medida (m², metro, unidade, hora, etc.) e preço base,
Para que eu possa selecionar rapidamente itens ao montar orçamentos para os clientes.

#### AC-045 — Catálogo e orçamento pertencem ao tenant

- **Dado** um prestador autenticado
- **Quando** cadastra itens do catálogo ou orçamentos
- **Então** Todos os itens do catálogo de serviços e orçamentos cadastrados devem pertencer obrigatoriamente ao `TenantId` do prestador autenticado (`ITenantOwned`).

A persistência do catálogo preserva identidade, tenant, nome, descrição opcional, unidade, preço base e estado ativo/inativo em contexto novo. Limites físicos existentes Name200/Description1000/Unit50 são validados antes de mutação/persistência, sem truncamento. Preço zero, duas casas e teto Q-020 continuam válidos. Inativação/atualização do catálogo não alteram valores ou vínculos históricos dos itens de orçamento/OS; exclusão de catálogo referenciado é Restrict. Filtros/guardas EF e RLS físico são barreiras independentes, incluindo ausência de tenant e tenant original/current em gravações. Matriz própria T-032 em .spec/matriz-t032.md; nenhuma conclusão automática das tarefas T-033 a T-037.

### US-021 — Criar Orçamento de Serviços/Produtos para Cliente Final
Como prestador de serviço autenticado,
Quero emitir um orçamento associado a um cliente existente, incluindo data de emissão, validade, itens com quantidade, preço unitário e desconto,
Para apresentar uma proposta comercial clara e detalhada ao cliente.

#### AC-049 — Orçamento de outra empresa é inacessível

- **Dado** um orçamento de outro tenant
- **Quando** um usuário tenta acessar, alterar ou exportar seu PDF
- **Então** Qualquer tentativa de acessar, alterar ou exportar PDF de orçamento pertencente a outro tenant deve retornar status HTTP 404/403.

### US-022 — Registrar Sinal / Entrada no Orçamento
Como prestador de serviço autenticado,
Quero definir e registrar a condição de pagamento de Sinal/Entrada (definido em porcentagem ou valor fixo),
Para formalizar o recebimento inicial exigido para iniciar a execução do serviço/fabricação.

#### AC-046 — Totais e composição da entrada são calculados

- **Dado** itens com quantidades, preços, descontos e sinal
- **Quando** o orçamento é calculado
- **Então** O orçamento deve calcular automaticamente os subtotais dos itens, descontos, valor total final e a composição do Sinal/Entrada (ex: valor da entrada e saldo restante).

### US-023 — Gerar e Exportar Orçamento em PDF
Como prestador de serviço autenticado,
Quero exportar e baixar o documento do orçamento formatado obrigatoriamente em PDF,
Para enviar diretamente ao cliente (via WhatsApp ou e-mail) com garantia de privacidade, sem dependência de links públicos na web.

#### AC-047 — PDF entregue por download privado

- **Dado** um orçamento do tenant autenticado
- **Quando** o prestador solicita sua exportação
- **Então** A exportação de orçamento deve ser disponibilizada exclusivamente via stream/download de arquivo PDF (`application/pdf`), atendendo rigorosamente à regra de negócio de não expor links públicos web.

### US-024 — Consultar e Atualizar Status do Orçamento
Como prestador de serviço autenticado,
Quero visualizar o histórico de orçamentos e atualizar seu status (Rascunho, Pendente, Aprovado, Rejeitado, Expirado),
Para ter controle do pipeline de vendas e negociações da empresa.

#### AC-048 — Transições respeitam regras de negócio

- **Dado** um orçamento sem itens ou expirado
- **Quando** o prestador tenta aprová-lo
- **Então** Transições de status do orçamento devem seguir validações de negócio (ex: não aprovar orçamento sem itens ou já expirado).


## Decisão de edição — 12/10/2026

O usuário permite edição enquanto o orçamento está pendente e bloqueia após aprovação, rejeição ou conversão. A edição é exposta por PUT `/api/v1/quotations/{id}`, substitui o conteúdo e recalcula itens, total e entrada, preservando identidade e status. A aprovação e a edição compartilham bloqueio de linha no PostgreSQL; ao aguardar uma aprovação concorrente, a edição lê o estado final e é rejeitada. Testes HTTP e PostgreSQL exclusivos cobrem persistência, orçamento enviado, isolamento e imutabilidade após decisões; o gate mecânico permanece responsável pela prova da feature.

## Regras financeiras confirmadas — T-031 — 12/10/2026

Decisões dadas pelo usuário nesta revisão, antes da implementação:
- Preços, desconto absoluto, valor do sinal, percentual e quantidade aceitam até duas casas; entradas com precisão excessiva são rejeitadas, sem arredondamento silencioso. Quantidade é positiva; preços e descontos podem ser zero.
- Valores monetários, subtotais/descontos/totais agregados e quantidade têm máximo 9.999.999.999.999.999,99, compatível com numeric(18,2) existente.
- Subtotal de cada item = quantidade × preço unitário, arredondado para duas casas, meio para longe de zero. Desconto não ultrapassa esse subtotal arredondado. Total = soma dos subtotais arredondados menos descontos.
- Sinal percentual entre 0 e 100, calculado sobre o total e arredondado pelo mesmo método; sinal fixo entre zero e total. Saldo restante = total menos sinal já arredondado, conservando o valor total.
- Quantidade tem as mesmas duas casas/teto para preservar conversão em OS, sem migration. Recebíveis e apresentação da OS devem consumir cálculos consistentes com o orçamento.
- Operação recusada preserva itens, conteúdo, totais, sinal e estado anteriores. Catálogo e cliente referenciados devem existir no tenant autenticado.

Ambiguidades de precisão/teto foram resolvidas explicitamente nesta sessão. Design e provas estão em design.md e na revisão T-031; não substituir o gate por existência de arquivos ou provas antigas.

## Revisão de estados — T-088 — 12/10/2026

Quotation.Create monta um Draft interno com TenantId/CustomerId não vazios e coleção somente de leitura. A criação pública valida cliente e catálogo no tenant autenticado, monta itens e sinal e chama MarkAsPending antes de persistir. O estado inicial observado por POST/GET é Pending (AC-090), sem expor uma criação pública Draft.

O domínio permite Draft → Pending com itens; Pending válido → Approved; Pending → Rejected; Approved → Converted. CheckExpiration somente altera Pending vencido para Expired, usando a data UTC e limite estrito anterior a hoje. Cancel mantém a política histórica: Draft/Pending/Expired → Canceled e repetição Canceled sem mudança; Approved/Rejected/Converted recusam cancelamento. Edição completa somente Pending permanece em T-103; montagem item/sinal somente Draft.

O handler bloqueia decisões em Pending vencido antes de mutar o agregado rastreado; cancelamento continua permitido. A divergência entre AC-093 e a aprovação repetida idempotente em Approved/Converted foi resolvida pelo usuário em Q-022: a exceção pública é permitida, sem alteração ou duplicação financeira/OS. Provas antigas da rejeição de Converted não certificam todas as decisões repetidas; os novos testes HTTP/PostgreSQL exercitam a exceção e as recusas. T-089 a T-092 e T-032 a T-037 permanecem pendentes de revisão própria.

## Fechamento T-089 — 13/10/2026

Mapeamento revisado e provado por 32 testes EF/metadata/guardas e sete PostgreSQL exclusivos, incluindo upgrade válido preservado e inválido transacional. Correções de filtros/guardas de dependentes e FK composta catálogo em design.md/.spec/revisao-t089.md. T-089 concluída; T-090 a T-092 e T-032 a T-037 permanecem pendentes. Renovação das 15 features: motor0/record.exitCode0, 354 PASS/zero falhas/seis skips antigos por execução (360 resultados). Gate final em .spec/audit-t089.json. Migração nova não aplicada ao banco de trabalho; contrato HTTP/schema frontend preservados.

## Contrato de gestão HTTP — T-090 — 13/10/2026

POST cria Pending com tenant exclusivamente da identidade, cliente obrigatório e catálogo opcional validados no tenant. Retorna 201 JSON `{id}` e Location relativo para o detalhe autenticado. GET listagem/detalhe exigem sales.read; POST/PUT/PATCH exigem sales.write. PDF exige sales.read e conversão exige sales.write, sem certificação integral dessas entregas por T-090.

GET aceita busca por código/nome do cliente sem distinguir maiúsculas, filtro por enum de status definido e paginação. Decisão técnica de proteção da consulta: Page >= 1, PageSize entre 1 e 100, padrões 1/20; deslocamento calculado em long e limitado a int.MaxValue. Entradas inválidas retornam 400 ProblemDetails seguro. Total é filtrado antes da paginação; página além do total é vazia. Ordem IssueDate DESC, Id ASC oferece desempate estável para dados inalterados (não promete snapshot entre requisições concorrentes).

POST/PUT validam os limites físicos existentes: Code obrigatório até 50, Notes/PaymentTerms opcionais até 2000, Deposit.PaymentNotes opcional até 1000. Datas devem ser UTC, incluindo validade opcional; data omitida/sem fuso é recusada antes de Npgsql. Código é único dentro do tenant; precheck antecede mutações e a restrição única resolve corrida com 400 seguro, sem registros parciais. Mesmo código em tenants distintos é permitido. PUT preserva edição exclusivamente Pending (T-103).

Binding inválido produz 400/415 application/problem+json; challenge 401 é vazio com WWW-Authenticate, permissão insuficiente retorna 403 ProblemDetails. IDs inexistentes/externos em detalhe/PUT/PATCH retornam 404 vazio; UUID malformado não casa rota e retorna 404. Sucesso de PUT/PATCH é 204 vazio. Decisões seguem AC-093/Q-022 e validação antes de mutação, sem duplicar recebíveis/OS. Contrato financeiro Q-020/Q-021, RLS/guardas, CPF, notificações e PDF permanecem.

T-090 concluída após revisão própria, sete provas novas PASS (HTTP InMemory/PostgreSQL restrito, rastreamento, contratos e corridas POST/PUT), renovação das 15 features motor=0/record.exitCode=0 (367 resultados, 361 PASS, zero falhas, seis skips históricos) e audit --ci exit0/95 critérios provados/zero erros e avisos. Evidências e limites em .spec/revisao-t090.md e .local-verification/t090/final-certified-summary.json. T-091/T-092 e T-032 a T-037 continuam pendentes, sem revisão própria; concorrência integral da conversão não certificada. Sem migrations novas/aplicadas ao banco de trabalho, serviço5050, commit/push/deploy ou executor headless.

## Contrato da conversão — T-091 — 14/10/2026

A nova conversão exige Approved; replay idempotente devolve a OS existente e conserva estado/conteúdo/recebíveis. As duas rotas conservam respostas diferentes: quotations/{id}/convert retorna200 {workOrderId}; work-orders/from-quotation/{quotationId} retorna201 {id} e Location, inclusive replay. Autenticação/sales.write e tenant da identidade protegem ambas, sem body de conversão. Malformado404 vazio; vazio/inexistente/externo400 seguro sem vazamento.

A OS copia cliente, tenant, vínculo do orçamento, código com prefixo OS-, notas e campos completos dos itens (catálogo opcional, nome, unidade, quantidade, preço, desconto). Identidades dos itens são novas. Totais derivam dos subtotais arredondados do orçamento; sinal/termos ficam no orçamento vinculado, recebíveis RequiredAmount existentes permanecem intactos. Scheduled/CreatedAt UTC/CompletedAt null aguardam início manual (ASM-016).

Transação e FOR UPDATE compartilhado com edição/aprovação antecedem as consultas; unicidade física por tenant/orçamento protege contra duplicação. Recusa antes da mutação preserva o agregado rastreado e SaveChanges posterior; erro após gravação relacional implica rollback das linhas e descarte do scope HTTP. Integridade do catálogo nullable/Restrict e RLS permanecem independentes do handler. Provas próprias abrangem concorrência das duas rotas com sessões simultaneamente bloqueadas e falhas controladas após writes, além de HTTP, isolamento e migrations exclusivas. Estado de conclusão e gate serão registrados somente após execução final.

## Fechamento T-091 — 14/10/2026

Somente T-091 concluída após revisão própria, 39 casos novos e certificação completa. As 15 features renovadas têm motor=0 e record.exitCode=0, cada uma com406 resultados (400PASS, zero falhas e seis skips antigos). Logs/TRX/JSON preservados em .local-verification/t091/certified-complete; índice final em final-certified-summary.json. Audit --ci exit0, ok=true,95/95 critérios provados, zero erros/avisos (.spec/audit-t091.json).

Frontend: schema regenerado pelo OpenAPI local após documentar Location201 da rota alternativa; documentação instalada Next.js16.2.11 consultada. Typecheckexit0 e232/232 testes PASS, sem alteração de componentes/BFF. Exportação OpenAPI hostInMemory exit0/1PASS; contrato observado200/workOrderId e201/id/Location preservado, inclusive replay.

T-092 e T-032 a T-037 permanecem pendentes; T-031/T-088/T-089/T-090/T-103 mantêm conclusão. Plano sequencial para sete tarefas regenerado somente como artefato; executor não iniciado e modelos históricos não adotados. Sem commit/push/deploy, serviço5050, entrega real WhatsApp/e-mail ou telas financeiro/equipe. Nova migration não aplicada ao banco de trabalho; quatro migrations continuam exigindo rollout explícito. Ver .spec/revisao-t091.md e docs/POSTGRESQL_RLS.md.

## Revisão de integração — T-092 — 14/10/2026

A cobertura conjunta de AC-090/091/092/093 foi revisada por camada, conforme .spec/matriz-t092.md. Domínio não certifica persistência; chamadas diretas a handlers com EF InMemory não certificam HTTP, transações, FKs ou RLS. Essas barreiras são provadas pelos testes HTTP/PostgreSQL restrito em bases temporárias exclusivas, incluindo sessões concorrentes observadas e falhas controladas após gravação.

A fixture QuotationTests agora usa o banco exclusivo de TeamFinancialTestFactory e sua guarda TenantSaveChangesInterceptor, com UoW e handler no mesmo DbContext. Leituras em contexto novo confirmam Pending, vínculos, itens com tenant, OS Scheduled/Converted e replay. O título histórico AC-093 descreve honestamente a rejeição de Converted; sua asserção permanece e foi ampliada com tracker Unchanged/SaveChanges posterior zero. Seis combinações finais comprovam recusa ou aprovação pública idempotente sem modificar o grafo rastreado.

A jornada integrada HTTP cria, edita Pending com troca de cliente/itens, aprova, repete aprovação, converte pelas duas rotas e repete decisões/conversão. Materialização nova comprova conteúdo editado, catálogo/manual gratuito, desconto, subtotal 15.045→15.05, entrada 7.505→7.51 e saldo7.50; recebíveis conservam identidades e valores. Domínio permanece estrito, Draft interno e Pending público. Contratos200/workOrderId e201/id/Location, Q-020/Q-021/Q-022, RLS, financeiro e início manual são preservados.

Reuso manual de contexto previamente carregado enquanto outro contexto grava e composição com transação externa não fazem parte dos scopes novos por request exercitados. Rollback relacional restaura linhas, não o ChangeTracker; não confundir com validação recusada antes de mutar. T-032 a T-037 continuam pendentes de revisão própria. Estado final e códigos de saída estão na revisão T-092, somente após os gates.

## Fechamento T-092 — 14/10/2026

Somente T-092 concluída após revisão própria das fixtures/cobertura conjunta, matriz requisito/cenário/prova, oito casos novos e ampliação das asserções antigas. As15features renovadas têm motor=0/record.exitCode=0,414 resultados por execução:408PASS, zero falhas e seis skips antigos. Provas completas em .local-verification/t092/certified e final-certified-summary.json; matriz .spec/matriz-t092.md, revisão .spec/revisao-t092.md e gate final .spec/audit-t092.json. Audit de revisão exit0/95 critérios provados/zero erros e avisos.

T-032 a T-037 permanecem pendentes de revisão própria; plano sequencial das seis tarefas regenerado somente como artefato, sem executar headless/modelos históricos. Nenhum contrato/produção/OpenAPI/frontend/migration alterado. Preservados cinco obj,2.080 evidências anteriores por hash, schema frontend e backup; sem commit/push/deploy, API5050 ou rollout no banco de trabalho.

## Fechamento T-032 — 14/10/2026

Somente T-032 concluída após revisão própria de catálogo/EF/migrations. Corrigidos limites Name200/Description1000/Unit50 antes de mutar, contando caracteres Unicode escalares como varchar PostgreSQL.62casos novos provam metadata efetiva, roundtrip integral em contexto novo, null/zero/centavos/teto/ativo-inativo, guardas root/batch original/current, HTTP seguro e RLS/constraints físicas. Inativação/atualização conserva agregados/itens/vínculos de orçamento e OS; catálogo referenciado é Restrict, avulsos permanecem nullable.

16migrations descobertas em ordem e HasPendingModelChanges=false; nenhuma migration nova. Reuso pertinente T-089/T-091 renovado inclui upgrades válidos e externo/órfão/duplicado recusados com rollback; prova nova preserva catálogo histórico completo. FKs compostas históricas SQL permanecem deliberadamente além do snapshot. Matriz .spec/matriz-t032.md e revisão .spec/revisao-t032.md identificam alcance e limites.

15features motor0/record0,476resultados por execução:470PASS, zero falhas e6skips antigos;149unitPASS,290integraçãoPASS+6skips,31arquiteturaPASS. Evidências .local-verification/t032/certified-complete e final-certified-summary.json. Audit de revisão --ci exit0/95critérios/zero erros e avisos; gate final .spec/audit-t032.json. Preservados cinco obj, schema/frontend/checks, dump, evidências e sinais anteriores (com adição legítima da tentativa recusada). Cleanup comprovado apenas de base/dois logins próprios de tentativa falha; recurso temporário anterior preservado.

T-033 a T-037 continuam pendentes de revisão própria. Nenhuma mudança DTO/rota/OpenAPI/frontend ou rollout no banco de trabalho; quatro migrations continuam não aplicadas. Sem commit/push/deploy, API5050, executor headless/modelos históricos, entrega real de mensagens ou telas financeiro/equipe.
