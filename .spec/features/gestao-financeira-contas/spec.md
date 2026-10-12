# Spec: Gestão financeira e contas

> feature: gestao-financeira-contas
> status: em-implementacao

## Contexto

O tenant precisa controlar suas despesas e valores a receber, registrando pagamentos e consultando o saldo. Os recebíveis são gerados na aprovação do orçamento, com entrada e saldo. Os pagamentos são registrados manualmente nesta etapa.

## Histórias

### US-048 — Lançar conta a receber

Como administrador, quero gerar contas a receber vinculadas à venda e ao cliente, para acompanhar o saldo devedor.

#### AC-094 — Geração de recebíveis vinculados

- **Dado** uma venda válida do tenant com valor total e eventual entrada
- **Quando** o orçamento for aprovado
- **Então** os títulos pendentes são persistidos no mesmo tenant, vinculados à origem e ao cliente, e sua soma corresponde ao total da venda; a repetição do evento não duplica títulos.

### US-049 — Lançar conta a pagar

Como administrador, quero registrar despesas, para acompanhar as obrigações do negócio.

#### AC-096 — Isolamento e acesso financeiro

- **Dado** dois tenants com registros financeiros próprios e usuários com permissões diferentes
- **Quando** consultam contas e extratos ou registram despesas e pagamentos
- **Então** cada usuário acessa somente seu tenant; consultas exigem financial.read e alterações exigem financial.write, retornando 403 quando a permissão falta e 404 para um registro de outro tenant.

### US-050 — Registrar pagamento

Como administrador, quero registrar pagamentos parciais ou totais, para manter os saldos corretos.

#### AC-095 — Baixa parcial ou total

- **Dado** uma conta a pagar ou receber pendente
- **Quando** um pagamento positivo e não superior ao saldo é registrado
- **Então** o saldo diminui, o pagamento é persistido e a conta fica paga ao atingir zero; valores inválidos são rejeitados sem alterar o saldo.

## Fora de escopo

- Telas do frontend nesta etapa.
- Integração bancária e gateway de pagamento (etapa futura).

## Suposições

| ID | Suposição | Status | Resolução |
|---|---|---|---|
| ASM-011 | As contas aceitam pagamentos parciais. | confirmada | Regra original mantida em AC-095. |

## Perguntas em aberto

| ID | Pergunta | Status | Resposta |
|---|---|---|---|
| Q-011 | O financeiro será manual ou terá gateway nesta etapa? | respondida | Usuário confirmou pagamentos manuais, sem gateway nesta etapa. |
| Q-012 | Recebíveis são gerados ao aprovar orçamento ou concluir OS? | respondida | Usuário confirmou aprovação do orçamento, com entrada e saldo. |

## Requisitos recuperados do histórico

Origem: commit 9012638. Os requisitos e tarefas antigos permanecem junto aos atuais; sua recuperação não certifica implementação nem RLS.

### US-029 — Lançamento Automático de Contas a Receber por Orçamento / OS
Como prestador de serviço autenticado,
Quero que a aprovação de um Orçamento com Sinal/Entrada gere automaticamente o título de Conta a Receber da entrada e os títulos das parcelas do saldo restante,
Para evitar erros manuais de digitação e garantir controle financeiro rigoroso.

#### AC-056 — Entrada gera título no valor configurado

- **Dado** um orçamento com sinal configurado
- **Quando** o orçamento é aprovado
- **Então** Na aprovação do orçamento, deve ser gerado o título a receber do Sinal no valor exato configurado, com vencimento imediato ou estipulado, conforme decisão do usuário em 12/10/2026.

### US-030 — Registrar e Liquidador Pagamentos (Baixa de Títulos)
Como prestador de serviço autenticado,
Quero registrar o recebimento total ou parcial de um título informando data de liquidação, valor pago e meio de pagamento (Pix, Dinheiro, Cartão),
Para atualizar o status financeiro do cliente e quitar o débito.

#### AC-057 — Baixa parcial acumula pagamentos até quitação

- **Dado** um título pendente com saldo
- **Quando** pagamentos parciais ou totais são registrados
- **Então** Um título pago parcialmente deve ter seu status atualizado para `PartiallyPaid`, acumulando o histórico de pagamentos até a quitação integral (`Paid`).

### US-031 — Cadastrar e Gerenciar Contas a Pagar (Despesas Operacionais)
Como prestador de serviço autenticado,
Quero cadastrar despesas da minha empresa (compra de insumo/matéria-prima, ferramentas, transporte, aluguel) com data de vencimento e fornecedor,
Para acompanhar as obrigações financeiras da empresa.

#### AC-055 — Registros financeiros pertencem ao tenant

- **Dado** um tenant autenticado
- **Quando** títulos e movimentações de caixa são cadastrados ou consultados
- **Então** Todos os títulos de Contas a Receber, Contas a Pagar e movimentações de caixa pertencem obrigatoriamente ao `TenantId` autenticado (`ITenantOwned`).

#### AC-058 — Títulos vencidos são sinalizados

- **Dado** um título Pending ou PartiallyPaid com vencimento anterior à data atual
- **Quando** o status de atraso é verificado
- **Então** Títulos com data de vencimento menor que a data atual e status `Pending` ou `PartiallyPaid` devem ser sinalizados/retornados como `Overdue` (Atrasado).

### US-032 — Visualizar Extrato Financeiro e Fluxo de Caixa do Tenant
Como prestador de serviço autenticado,
Quero consultar o extrato de movimentações (Entradas x Saídas) por período, filtrando por status (Pendente, Pago, Atrasado),
Para entender a saúde financeira e previsão de caixa do meu negócio.

#### AC-059 — Extrato consolida totais por período

- **Dado** registros financeiros de um período
- **Quando** o prestador consulta o extrato
- **Então** Consultas de extrato financeiro devem retornar os somatórios consolidados de total a receber, total a pagar e saldo líquido do período pesquisado.
