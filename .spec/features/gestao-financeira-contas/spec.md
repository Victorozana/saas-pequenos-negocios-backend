# Feature Spec: Gestão Financeira e Contas a Receber / Pagar

## Visão Geral
Esta feature permite o controle financeiro completo do prestador de serviço e pequeno negócio. Contempla o lançamento automático de Contas a Receber originadas dos Orçamentos (Sinal/Entrada e saldo restante), registro de receitas avulsas, controle de Contas a Pagar (despesas operacionais) e liquidação/baixa de lançamentos com suporte a múltiplos métodos de pagamento (Pix, Cartão, Dinheiro, Transferência).

## User Stories (US)

### US-029: Lançamento Automático de Contas a Receber por Orçamento / OS
Como prestador de serviço autenticado,  
Quero que a aprovação de um Orçamento com Sinal/Entrada gere automaticamente o título de Conta a Receber da entrada e os títulos das parcelas do saldo restante,  
Para evitar erros manuais de digitação e garantir controle financeiro rigoroso.

### US-030: Registrar e Liquidador Pagamentos (Baixa de Títulos)
Como prestador de serviço autenticado,  
Quero registrar o recebimento total ou parcial de um título informando data de liquidação, valor pago e meio de pagamento (Pix, Dinheiro, Cartão),  
Para atualizar o status financeiro do cliente e quitar o débito.

### US-031: Cadastrar e Gerenciar Contas a Pagar (Despesas Operacionais)
Como prestador de serviço autenticado,  
Quero cadastrar despesas da minha empresa (compra de insumo/matéria-prima, ferramentas, transporte, aluguel) com data de vencimento e fornecedor,  
Para acompanhar as obrigações financeiras da empresa.

### US-032: Visualizar Extrato Financeiro e Fluxo de Caixa do Tenant
Como prestador de serviço autenticado,  
Quero consultar o extrato de movimentações (Entradas x Saídas) por período, filtrando por status (Pendente, Pago, Atrasado),  
Para entender a saúde financeira e previsão de caixa do meu negócio.

## Critérios de Aceite (AC)

- **AC-055**: Todos os títulos de Contas a Receber, Contas a Pagar e movimentações de caixa pertencem obrigatoriamente ao `TenantId` autenticado (`ITenantOwned`).
- **AC-056**: Na criação do Sinal/Entrada do Orçamento, deve ser gerado o título a receber do Sinal no valor exato configurado, com vencimento imediato ou estipulado.
- **AC-057**: Um título pago parcialmente deve ter seu status atualizado para `PartiallyPaid`, acumulando o histórico de pagamentos até a quitação integral (`Paid`).
- **AC-058**: Títulos com data de vencimento menor que a data atual e status `Pending` ou `PartiallyPaid` devem ser sinalizados/retornados como `Overdue` (Atrasado).
- **AC-059**: Consultas de extrato financeiro devem retornar os somatórios consolidados de total a receber, total a pagar e saldo líquido do período pesquisado.
