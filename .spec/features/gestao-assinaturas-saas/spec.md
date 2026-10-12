# Feature Spec: Gestão de Planos, Assinaturas e Billing do SaaS

> feature: gestao-assinaturas-saas
> status: em-implementacao

## Visão Geral
Esta feature permite administrar os Planos do próprio SaaS, a assinatura dos Tenants (Empresas Clientes), os limites de recursos por plano (ex: limite de orçamentos por mês, limite de membros na equipe) e o ciclo de vida da cobrança da mensalidade do SaaS (Active, Trial, PastDue, Canceled) com recebimento de Webhooks de pagamento.

## User Stories (US)

### US-201 — Cadastrar e Configurar Planos de Assinatura do SaaS
Como administrador global da plataforma SaaS,  
Quero cadastrar planos (ex: Básico, Pro, Enterprise) definindo valor mensal/anual e limites de recursos (ex: max de membros, max de orçamentos/mês),  
Para oferecer diferentes modalidades de contratação aos prestadores de serviço.

#### AC-201 — Assinatura isolada e alteração autorizada

- **Dado** as informações de assinatura de um tenant
- **Quando** são consultadas ou é solicitada uma alteração de plano
- **Então** pertencem estritamente ao contexto de billing desse tenant e a alteração só pode ser solicitada pelo admin do tenant ou webhook do gateway.

### US-202 — Gerenciar Assinatura e Ciclo de Vida do Tenant
Como proprietário do tenant autenticado,  
Quero visualizar o meu plano atual, data de renovação da assinatura, histórico de faturas do SaaS e status da conta (`Trialing`, `Active`, `PastDue`, `Canceled`),  
Para acompanhar a regularidade do meu acesso à plataforma.

#### AC-204 — Escrita bloqueada após carência da inadimplência

- **Dado** uma assinatura em `PastDue`
- **Quando** termina a carência configurável (por exemplo, três dias)
- **Então** o acesso às funcionalidades de escrita é bloqueado.

#### AC-205 — Expiração automática do período de testes

- **Dado** um tenant em `Trialing` sem contratação de plano pago
- **Quando** passam 14 dias da criação do tenant
- **Então** a assinatura de teste expira automaticamente.

### US-203 — Validar Limites de Uso do Plano (Feature Gating)
Como sistema SaaS,  
Quero barrar a execução de ações (ex: cadastrar novo membro ou criar orçamento além do limite) caso o tenant tenha atingido a cota do seu plano contratado,  
Para incentivar o upgrade de plano e garantir sustentabilidade do negócio.

#### AC-202 — Cotas impedem criação excedente

- **Dado** um tenant que atingiu o limite de recursos do plano
- **Quando** tenta criar um recurso além da cota
- **Então** recebe HTTP 402 ou 403 com mensagem instrutiva de upgrade.

### US-204 — Processar Webhooks de Pagamento de Assinatura do SaaS
Como sistema SaaS,  
Quero receber webhooks de confirmação de pagamento ou falha de cobrança do gateway de pagamento (Stripe/Asaas),  
Para atualizar instantaneamente o status da assinatura do tenant para `Active` ou `PastDue`.

#### AC-203 — Webhooks atualizam assinatura de forma idempotente

- **Dado** uma notificação de pagamento ou falha de cobrança do gateway
- **Quando** a notificação é recebida, inclusive repetidamente
- **Então** o status da assinatura é atualizado de forma idempotente.

## Suposições

Nenhuma nova suposição nesta normalização.

## Perguntas em aberto

A escolha e integração do gateway estão adiadas por decisão do usuário em 12/10/2026. Os requisitos de billing permanecem pendentes e não são certificados por pagamento manual de clientes.
