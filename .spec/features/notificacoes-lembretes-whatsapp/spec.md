# Feature Spec: Notificações e Lembretes WhatsApp

> feature: notificacoes-lembretes-whatsapp
> status: em-implementacao

## Visão Geral
Esta feature provê um mecanismo genérico e extensível para enviar notificações de sistema (e futuramente WhatsApp/Email) aos clientes, como lembretes de agendamentos e faturas atrasadas.

## User Stories (US)

### US-051 — Lembrete de Agendamento
Como prestador de serviços,
Quero que o sistema gere notificações automáticas para meus clientes sobre agendamentos próximos,
Para reduzir a taxa de não-comparecimento (no-show).

#### AC-097 — Serviço centralizado abstrai o provedor

- **Dado** uma notificação do sistema
- **Quando** o envio é solicitado
- **Então** o serviço centralizado `INotificationService` abstrai o provedor (Console/Email/WhatsApp).

#### AC-098 — Histórico vinculado ao tenant e cliente

- **Dado** uma notificação enviada
- **Quando** o envio é registrado para auditoria
- **Então** o histórico é registrado por tenant e vinculado ao cliente.

#### AC-099 — Agendamento futuro registra intenção de notificar

- **Dado** um serviço para uma data futura
- **Quando** o serviço é agendado
- **Então** o sistema enfileira/registra a intenção de notificar o cliente.

#### AC-207 — Lembretes podem ser desativados por cliente

- **Dado** dois clientes do mesmo tenant, um com lembretes automáticos desativados
- **Quando** o sistema prepara lembretes automáticos de agendamento
- **Então** não enfileira lembrete para o cliente desativado e mantém os lembretes do outro cliente, sem afetar outras empresas.

### US-052 — Aviso de Fatura Vencida
Como prestador de serviços,
Quero que o sistema notifique clientes com contas atrasadas,
Para melhorar o meu recebimento financeiro.

Esta história ainda não possui critério de aceite executável para o disparo de avisos de atraso. A lacuna permanece na auditoria.

## Suposições

| ID | Suposição | Status |
| --- | --- | --- |
| ASM-012 | Inicialmente a notificação é gravada no banco, simulando envio, sem API real de WhatsApp. | aberta |

## Perguntas em aberto

| ID | Pergunta | Status | Resposta |
| --- | --- | --- |
| Q-017 | O prestador poderá desligar notificações automáticas para determinados clientes? | respondida | Sim, por cliente; usuário confirmou em 12/10/2026. |

## Requisitos recuperados do histórico

Origem: commit cdcd95b. Os requisitos e tarefas antigos permanecem junto aos atuais; sua recuperação não certifica implementação nem RLS.

### US-033 — Enviar Lembrete Automático de Agendamento ao Cliente
Como prestador de serviço autenticado,
Quero que o sistema programe o envio automático de lembretes (ex: 24h ou 2h antes) para o cliente sobre a visita técnica ou instalação agendada,
Para reduzir a taxa de ausência/imprevistos no local de atendimento.

#### AC-062 — Templates configuráveis por tenant

- **Dado** uma mensagem com variáveis de cliente, agenda ou orçamento
- **Quando** o sistema prepara o envio
- **Então** O envio de mensagens deve suportar parâmetros configuráveis por tenant (templates de mensagens com variáveis como `{ClienteNome}`, `{DataAgendamento}`, `{CodigoOrcamento}`).

### US-034 — Notificar Cliente sobre Emissão / Expiração de Orçamento
Como prestador de serviço autenticado,
Quero disparar uma mensagem direta para o WhatsApp do cliente contendo o resumo da proposta comercial e aviso de vencimento,
Para agilizar a tomada de decisão e aprovação do orçamento.

#### AC-064 — Mensagem não expõe dados internos sensíveis

- **Dado** uma notificação de orçamento
- **Quando** o sistema prepara o payload enviado
- **Então** O payload da mensagem enviada nunca deve expor tokens de acesso ou dados sensíveis internos do tenant.

### US-035 — Registrar Fila de Notificações (Outbox Pattern)
Como arquiteto do sistema,
Quero que todos os eventos de notificação sejam armazenados em uma tabela de Outbox antes do envio externo,
Para garantir resiliência, retry automático e evitar perda de mensagens em instabilidade do provedor de comunicação.

#### AC-061 — Outbox processada em background

- **Dado** uma mensagem disparada pelo sistema
- **Quando** seu envio é processado
- **Então** Mensagens disparadas pelo sistema devem ser processadas via padrão Outbox (tabela `NotificationOutbox`) em background sem bloquear as requisições HTTP da API.

#### AC-063 — Falha provoca retry com backoff

- **Dado** uma mensagem aguardando envio
- **Quando** o gateway falha
- **Então** Em caso de falha de comunicação com o gateway/provedor externo, o sistema deve registrar a tentativa, incrementar a contagem de retries e agendar a nova tentativa com backoff exponencial.

### US-036 — Consultar Histórico e Status de Envios por Cliente / Tenant
Como prestador de serviço autenticado,
Quero visualizar o histórico de mensagens e lembretes enviados a um cliente específico e o status de entrega (Pendente, Enviado, Falha),
Para ter registro de todas as interações e mensagens transmitidas.

#### AC-060 — Histórico pertence ao tenant

- **Dado** um tenant autenticado
- **Quando** suas notificações são registradas ou consultadas
- **Então** Todas as notificações e mensagens registradas pertencem obrigatoriamente ao `TenantId` autenticado (`ITenantOwned`).
