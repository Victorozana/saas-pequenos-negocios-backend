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
