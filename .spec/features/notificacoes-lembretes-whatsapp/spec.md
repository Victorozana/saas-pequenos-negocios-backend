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

A preferência `AutomaticRemindersEnabled` é persistida por cliente e exposta no cadastro,
edição, lista e detalhe. Clientes existentes e cadastros que omitem o campo continuam
habilitados; edições que omitem o campo preservam a escolha atual. Tanto a intenção automática
na criação da agenda quanto o handler de lembretes passam pela verificação central antes de
enfileirar. A desativação não bloqueia mensagens manuais, orçamento nem convites System.
O critério trata da preparação de novos lembretes; não altera o histórico já enfileirado.

### US-052 — Aviso de Fatura Vencida
Como prestador de serviços,
Quero que o sistema notifique clientes com contas atrasadas,
Para melhorar o meu recebimento financeiro.

#### AC-208 — Aviso automático diário de conta vencida com saldo aberto

- **Dado** contas a receber de duas empresas, com vencimento anterior à data de processamento e saldo aberto maior que zero, não canceladas e vinculadas a clientes com lembretes automáticos habilitados
- **Quando** o processamento automático diário é executado, inclusive repetido ou reiniciado no mesmo dia
- **Então** é enfileirado no máximo um aviso por conta e dia, vinculado ao tenant e cliente corretos; em um dia seguinte a conta ainda vencida e com saldo aberto recebe novo aviso, até quitar ou cancelar, sem duplicar o aviso do dia nem afetar a outra empresa.

Contas com vencimento hoje/futuro, quitadas, canceladas ou sem saldo aberto não geram aviso.
O opt-out de lembretes automáticos do cliente também será respeitado neste novo produtor.
Retries de entrega reutilizam o registro do aviso, sem constituir novo disparo diário.
O produtor grava a intenção na outbox; a entrega permanece simulada pelo adaptador atual.
A comparação de vencimento deve seguir a regra de calendário financeiro; o horário operacional
do job não foi fixado nesta decisão. Os testes devem controlar a data de processamento.

## Suposições

| ID | Suposição | Status |
| --- | --- | --- |
| ASM-012 | Inicialmente a notificação é gravada no banco, simulando envio, sem API real de WhatsApp. | aberta |

## Perguntas em aberto

| ID | Pergunta | Status | Resposta |
| --- | --- | --- |
| Q-017 | O prestador poderá desligar notificações automáticas para determinados clientes? | respondida | Sim, por cliente; usuário confirmou em 12/10/2026. |
| Q-018 | Qual disparo gera o aviso de conta vencida? | respondida | Automático diário, somente conta vencida e com saldo aberto; usuário confirmou em 12/10/2026. |
| Q-019 | O aviso se repete enquanto a conta continua aberta? | respondida | Um aviso por conta por dia, até quitar ou cancelar; usuário confirmou em 12/10/2026. |

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

## Semântica do histórico e mensagens sem cliente — 12/10/2026

O vínculo de cliente é obrigatório para notificações de cliente: confirmação/lembrete de agenda,
notificação de orçamento e envio manual explicitamente associado a um cliente. Não pode ser
omitido nesses fluxos nem substituído pelo destinatário. O serviço valida o cliente no tenant
atual antes de gravar; a FK composta (tenant_id, CustomerId) reforça essa regra no PostgreSQL.

A rota manual avulsa existente continua aceitando destinatário livre quando CustomerId não é
informado. Essas mensagens são classificadas como Standalone e aparecem somente no histórico
geral do tenant, nunca como interação de um cliente nem como prova do vínculo exigido em AC-098.
Convites de equipe são System e também não são interações de clientes. Ambas as classificações
proíbem CustomerId. Informar cliente inexistente/de outra empresa retorna 400 sem gravação;
não há fallback para mensagem avulsa quando o vínculo informado é inválido.

Mensagens anteriores à migration são LegacyUnlinked, preservando todos os campos originais.
Não há evidência histórica suficiente para vincular essas mensagens: telefone/e-mail pode ser
compartilhado, alterado ou corresponder a convite. A migration não infere vínculos nem reclassifica
esse legado como histórico de cliente. Novas mensagens não podem usar LegacyUnlinked pelo serviço.
Essa limitação explícita do legado não dispensa o vínculo de nenhuma nova notificação de cliente.

O histórico expõe CustomerId e Audience e aceita filtro customerId, sempre sob o tenant
autenticado. O envio atual segue simulado; Sent registra sucesso do adaptador simulado,
sem certificar entrega real em WhatsApp ou e-mail.
