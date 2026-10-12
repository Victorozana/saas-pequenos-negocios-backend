# Design: serviço central e histórico de notificações

Implementação manual sequencial, com modelo/esforço atuais, sem agentes ou executores.

## Responsabilidades

INotificationService é a entrada dos casos de uso. QueueForCustomerAsync valida contexto e
cliente no tenant e adiciona NotificationMessage à outbox do DbContext da operação.
QueueStandalone e QueueSystem identificam explicitamente mensagens sem cliente.
Os métodos apenas preparam a gravação: SaveChanges permanece com o caso de uso para que
agenda/convite e outbox sejam confirmados juntos, sem envio externo na requisição HTTP.

NotificationOutboxProcessor mantém polling de 30 segundos e escopo por lote. Delega
ProcessPendingAsync ao serviço, que entra no escopo administrativo restrito, busca até 50
mensagens Pending elegíveis e usa ICommunicationGateway. O gateway é o adaptador substituível
do provedor; o mock continua simulando sucesso e registra apenas ID/canal. Não é uma renomeação
do gateway: validação do cliente, staging, seleção da fila e atualização de entrega/retries
ficam no serviço central. Falha/false preservam backoff de 2/4 minutos e limite de três tentativas.
Cancelamento do host não conta como falha do provedor. Exceções do provedor persistem motivo
sanitizado, sem copiar payload/credenciais da exceção.

## Persistência e migração

CustomerId é nullable somente para Standalone, System e LegacyUnlinked; CHECK exige vínculo
para Customer. A FK composta para Customers(tenant_id, Id) usa Restrict, preservando histórico
contra exclusão do cliente e rejeitando vínculos entre empresas. Índice começa por tenant_id,
CustomerId e CreatedAt. RLS existente permanece ENABLE/FORCE com suas policies e grants.

A migration adiciona Audience=LegacyUnlinked às linhas antigas, sem procurar por contato.
Remove o default após o backfill, adiciona CustomerId nullable e instala CHECK/FK/índice.
Não remove linhas nem muda status, destinatário, conteúdo, timestamps, retries ou erros antigos.
A chave alternativa EF representa o alvo composto da FK; o índice histórico de RLS permanece.

## Contrato

GET /api/v1/notifications/ aceita customerId opcional e retorna CustomerId/Audience.
POST send-manual aceita CustomerId opcional; ausência é avulsa explícita, vínculo inválido não
faz fallback. Os handlers de agenda/orçamento sempre usam QueueForCustomerAsync.
Convites usam QueueSystem. OpenAPI e schema frontend são regenerados a partir da aplicação.

## Provas

NotificationServiceTests: separação queue/envio, troca de gateway, worker real, tenants,
retries/backoff, cancelados, templates e rejeição antes de staging.
NotificationHistoryHttpTests: isolamento HTTP, filtro de cliente, avulsas com mesmo destinatário,
vínculos inválidos sem gravações e identidade preservada após envio.
NotificationHistoryPostgreSqlTests: duas bases exclusivas migradas a partir de RLS, comparação
integral dos campos antigos, FK/CHECK/Restrict sob login restrito e envio/histórico via serviço.
As provas temporárias não aplicam nenhuma migration a agendamento_local.

## Decisões e limites

Avulsas e convites já eram casos sem cliente; a classificação preserva essa compatibilidade
sem apresentá-los como histórico de cliente. Legado sem origem confiável fica identificado.
US-052 possui produtor diário e provas PostgreSQL de AC-208. Envio real permanece
fora da etapa. Auditoria global só pode ser aprovada quando os requisitos restantes tiverem prova.

## Preferência de lembretes por cliente (AC-207)

Customer.AutomaticRemindersEnabled inicia true. Cadastro aceita boolean com default true;
edição aceita boolean nullable e preserva a preferência quando omitida. Lista/detalhe retornam
o valor persistido junto ao CPF mascarado. PreserveDocument continua independente da preferência.
QueueAutomaticReminderAsync valida cliente/tenant e retorna null sem staging se desabilitado.
CreateAppointmentHandler e ScheduleAppointmentReminderHandler usam essa entrada; os fluxos de
manual/orçamento/System permanecem nas entradas existentes. A outbox histórica não é alterada.
Migration acrescenta coluna obrigatória com backfill true e retira o default transitório.
Prova PostgreSQL compara todos os campos antigos e verifica persistência/RLS em base exclusiva.

## Aviso diário de conta vencida (US-052 / AC-208)

OverdueReceivableReminderWorker reconcilia ao iniciar e a cada minuto, sem fixar horário de
operação. O período define a descoberta; a chave durável define um único aviso diário.
OverdueReceivableReminderProcessor usa TimeProvider.GetUtcNow e DateOnly, seguindo o
calendário financeiro existente (DueDate UTC, comparação por dia UTC). Executa no escopo
administrativo restrito; um contexto de tenant presente limita a seleção àquela empresa.

INotificationService.QueueOverdueReceivableReminderAsync bloqueia a conta com FOR UPDATE
numa transação, relê saldo/pagamentos/status e valida cliente da mesma empresa e opt-out.
Sem cliente confiável ou telefone válido não fabrica vínculo nem destinatário. Apenas a
outbox é gravada/confirmada; o gateway permanece no despacho, com retries da mesma mensagem.
O índice único tenant_id/ReceivableTitleId/ReminderDate protege também outros escritores.
FK composta para conta e CHECK de par de campos preservam isolamento e integridade.
Linhas antigas recebem dois campos nullable, mantendo integralmente o histórico anterior.
O papel de sistema recebe leitura de recebíveis/pagamentos e permissão de UPDATE(Id) somente
para bloqueio: a policy de UPDATE usa WITH CHECK false, rejeitando gravações financeiras.
O papel normal continua limitado pelas mesmas policies/guarda do tenant.

Provas PostgreSQL usam bases exclusivas e data controlada: repetição, reinício, concorrência
observada por pg_stat_activity, dia seguinte, pagamento parcial, quitação/cancelamento,
vencimento hoje/futuro, saldo zero, opt-out, outras contas/empresas e upgrade do histórico.
A migration nova não é aplicada ao banco de trabalho nesta tarefa.
