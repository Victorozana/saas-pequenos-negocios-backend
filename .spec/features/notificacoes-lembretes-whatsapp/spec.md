# Feature Spec: Notificações, Lembretes e Integração WhatsApp

## Visão Geral
Esta feature permite o envio de comunicações operacionais e comerciais automatizadas do prestador de serviço para seus clientes finais. O módulo disponibiliza suporte a outbox de notificações, envio de lembretes prévios de visitas técnicas/instalações e notificações de aviso de orçamento emitido/pendente ou recibo de entrada via integração estruturada para WhatsApp / E-mail.

## User Stories (US)

### US-033: Enviar Lembrete Automático de Agendamento ao Cliente
Como prestador de serviço autenticado,  
Quero que o sistema programe o envio automático de lembretes (ex: 24h ou 2h antes) para o cliente sobre a visita técnica ou instalação agendada,  
Para reduzir a taxa de ausência/imprevistos no local de atendimento.

### US-034: Notificar Cliente sobre Emissão / Expiração de Orçamento
Como prestador de serviço autenticado,  
Quero disparar uma mensagem direta para o WhatsApp do cliente contendo o resumo da proposta comercial e aviso de vencimento,  
Para agilizar a tomada de decisão e aprovação do orçamento.

### US-035: Registrar Fila de Notificações (Outbox Pattern)
Como arquiteto do sistema,  
Quero que todos os eventos de notificação sejam armazenados em uma tabela de Outbox antes do envio externo,  
Para garantir resiliência, retry automático e evitar perda de mensagens em instabilidade do provedor de comunicação.

### US-036: Consultar Histórico e Status de Envios por Cliente / Tenant
Como prestador de serviço autenticado,  
Quero visualizar o histórico de mensagens e lembretes enviados a um cliente específico e o status de entrega (Pendente, Enviado, Falha),  
Para ter registro de todas as interações e mensagens transmitidas.

## Critérios de Aceite (AC)

- **AC-060**: Todas as notificações e mensagens registradas pertencem obrigatoriamente ao `TenantId` autenticado (`ITenantOwned`).
- **AC-061**: Mensagens disparadas pelo sistema devem ser processadas via padrão Outbox (tabela `NotificationOutbox`) em background sem bloquear as requisições HTTP da API.
- **AC-062**: O envio de mensagens deve suportar parâmetros configuráveis por tenant (templates de mensagens com variáveis como `{ClienteNome}`, `{DataAgendamento}`, `{CodigoOrcamento}`).
- **AC-063**: Em caso de falha de comunicação com o gateway/provedor externo, o sistema deve registrar a tentativa, incrementar a contagem de retries e agendar a nova tentativa com backoff exponencial.
- **AC-064**: O payload da mensagem enviada nunca deve expor tokens de acesso ou dados sensíveis internos do tenant.
