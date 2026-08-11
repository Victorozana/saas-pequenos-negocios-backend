# Feature Spec: Gestão de Orçamentos e Serviços

## Visão Geral
Esta feature permite ao prestador de serviços criar orçamentos para seus clientes, adicionar serviços ao orçamento e aprovar ou rejeitar o orçamento, transformando-o posteriormente em ordem de serviço.

## User Stories (US)

### US-045: Criar Orçamento para um Cliente
Como prestador de serviços,
Quero criar um orçamento associado a um cliente contendo os serviços prestados e seus valores,
Para enviar uma estimativa de custos formalizada.

### US-046: Aprovar ou Rejeitar Orçamento
Como prestador de serviços,
Quero poder alterar o status de um orçamento para Aprovado ou Rejeitado,
Para ter o histórico do funil de vendas.

### US-047: Transformar Orçamento Aprovado em Ordem de Serviço
Como prestador de serviços,
Quero gerar uma Ordem de Serviço (Work Order) a partir de um orçamento aprovado,
Para iniciar a execução dos serviços sem retrabalho de digitação.

## Critérios de Aceite (AC)

- **AC-090**: Ao criar um orçamento, o status inicial deve ser `Pendente` e o valor total deve ser a soma dos valores dos serviços incluídos.
- **AC-091**: Apenas orçamentos com status `Aprovado` podem ser convertidos em Ordens de Serviço.
- **AC-092**: Orçamentos de um tenant não podem ser acessados ou alterados por usuários de outro tenant.
- **AC-093**: Não é possível aprovar ou rejeitar um orçamento que já foi convertido em Ordem de Serviço ou que já está aprovado/rejeitado.

## Suposições e Perguntas

- **ASM-010**: A conversão para ordem de serviço apenas copiará os serviços e criará a O.S. (Confirmada implicitamente pelo escopo).
- **Q-010**: O orçamento pode ser editado depois de enviado para o cliente? (aberta)
