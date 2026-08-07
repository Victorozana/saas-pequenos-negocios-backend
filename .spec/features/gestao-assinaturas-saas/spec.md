# Feature Spec: Gestão de Planos, Assinaturas e Billing do SaaS

## Visão Geral
Esta feature permite administrar os Planos do próprio SaaS, a assinatura dos Tenants (Empresas Clientes), os limites de recursos por plano (ex: limite de orçamentos por mês, limite de membros na equipe) e o ciclo de vida da cobrança da mensalidade do SaaS (Active, Trial, PastDue, Canceled) com recebimento de Webhooks de pagamento.

## User Stories (US)

### US-041: Cadastrar e Configurar Planos de Assinatura do SaaS
Como administrador global da plataforma SaaS,  
Quero cadastrar planos (ex: Básico, Pro, Enterprise) definindo valor mensal/anual e limites de recursos (ex: max de membros, max de orçamentos/mês),  
Para oferecer diferentes modalidades de contratação aos prestadores de serviço.

### US-042: Gerenciar Assinatura e Ciclo de Vida do Tenant
Como proprietário do tenant autenticado,  
Quero visualizar o meu plano atual, data de renovação da assinatura, histórico de faturas do SaaS e status da conta (`Trialing`, `Active`, `PastDue`, `Canceled`),  
Para acompanhar a regularidade do meu acesso à plataforma.

### US-043: Validar Limites de Uso do Plano (Feature Gating)
Como sistema SaaS,  
Quero barrar a execução de ações (ex: cadastrar novo membro ou criar orçamento além do limite) caso o tenant tenha atingido a cota do seu plano contratado,  
Para incentivar o upgrade de plano e garantir sustentabilidade do negócio.

### US-044: Processar Webhooks de Pagamento de Assinatura do SaaS
Como sistema SaaS,  
Quero receber webhooks de confirmação de pagamento ou falha de cobrança do gateway de pagamento (Stripe/Asaas),  
Para atualizar instantaneamente o status da assinatura do tenant para `Active` ou `PastDue`.

## Critérios de Aceite (AC)

- **AC-070**: As informações de assinatura do tenant pertencem estritamente ao seu contexto de billing, e a alteração de plano só pode ser solicitada pelo admin do tenant ou webhook do gateway.
- **AC-071**: Tentativas de criação de recursos excedendo os limites configurados no plano do tenant devem retornar erro HTTP 402 Payment Required ou 403 Forbidden com mensagem instrutiva de upgrade.
- **AC-072**: O status da assinatura do tenant deve ser atualizado de forma idempotente ao receber notificações de webhook do gateway de pagamento.
- **AC-073**: Caso a assinatura entre em status `PastDue` (Inadimplente), o acesso às funcionalidades de escrita do sistema deve ser bloqueado após carência configurável (ex: 3 dias).
- **AC-074**: A assinatura em período de testes (`Trialing`) deve expirar automaticamente após 14 dias da criação do tenant caso não ocorra contratação de um plano pago.
