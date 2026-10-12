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
