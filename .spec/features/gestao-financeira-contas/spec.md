# Feature Spec: Gestão Financeira e Contas

## Visão Geral
Esta feature permite o controle financeiro básico do tenant (prestador de serviço), incluindo contas a pagar/receber vinculadas aos serviços/O.S. executados.

## User Stories (US)

### US-048: Lançar Conta a Receber
Como prestador de serviços,
Quero gerar um registro de conta a receber a partir de uma O.S. finalizada,
Para acompanhar o que o cliente me deve.

### US-049: Lançar Conta a Pagar
Como prestador de serviços,
Quero registrar despesas e contas a pagar do meu negócio,
Para gerir o fluxo de caixa.

### US-050: Baixar Pagamento (Conciliação)
Como prestador de serviços,
Quero registrar o pagamento parcial ou total de uma conta (pagar/receber),
Para atualizar meu saldo e status financeiro.

## Critérios de Aceite (AC)

- **AC-094**: Ao criar uma conta a receber derivada de uma O.S., o valor da conta deve ser igual ao valor da O.S., e o status deve ser `Pendente`.
- **AC-095**: A baixa de pagamento deve abater do saldo devedor. Se o saldo chegar a zero, a conta muda para `Pago`.
- **AC-096**: Registros financeiros devem ser isolados por tenant e vinculados ao cliente (quando aplicável).

## Suposições e Perguntas

- **ASM-011**: As contas podem ter baixa parcial. (Confirmado pela AC-095).
- **Q-011**: Vamos integrar com algum gateway de pagamento real nesta feature ou é apenas fluxo de caixa manual? (aberta)
