# Feature Spec: Notificações e Lembretes WhatsApp

## Visão Geral
Esta feature provê um mecanismo genérico e extensível para enviar notificações de sistema (e futuramente WhatsApp/Email) aos clientes, como lembretes de agendamentos e faturas atrasadas.

## User Stories (US)

### US-051: Lembrete de Agendamento
Como prestador de serviços,
Quero que o sistema gere notificações automáticas para meus clientes sobre agendamentos próximos,
Para reduzir a taxa de não-comparecimento (no-show).

### US-052: Aviso de Fatura Vencida
Como prestador de serviços,
Quero que o sistema notifique clientes com contas atrasadas,
Para melhorar o meu recebimento financeiro.

## Critérios de Aceite (AC)

- **AC-097**: O sistema deve possuir um serviço centralizado de notificações (`INotificationService`) que abstraia o provedor de envio (Console/Email/WhatsApp).
- **AC-098**: O histórico de notificações enviadas deve ser registrado por tenant e vinculado ao cliente para auditoria.
- **AC-099**: Ao agendar um serviço para uma data futura, o sistema deve enfileirar/registrar a intenção de notificar o cliente.

## Suposições e Perguntas

- **ASM-012**: Inicialmente, a notificação será apenas gravada no banco (simulando envio) sem integração real com API do WhatsApp (ex: Twilio), para não gerar custos durante o desenvolvimento.
- **Q-012**: O prestador de serviço poderá desligar notificações automáticas para determinados clientes? (aberta)
