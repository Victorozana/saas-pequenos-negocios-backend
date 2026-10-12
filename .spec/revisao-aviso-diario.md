# Aviso diário de conta vencida — 12/10/2026

Backend main/96875bb e frontend master/ca1d485 preservados, sem commit/push/deploy.
Execução manual mantendo modelo/esforço, com agentes auxiliares autorizados para provas
e revisão de T-023. Alterações anteriores, obj, schema, sinais e relatórios preservados.

## Implementação AC-208 / T-105

Worker dedicado reconcilia ao iniciar e a cada minuto. Não fixa horário operacional: usa
o dia UTC do calendário financeiro existente, com TimeProvider controlável nos testes.
Somente recebível vencido antes do dia atual, com saldo positivo, não Paid/Canceled,
cliente explícito da mesma empresa e AutomaticRemindersEnabled habilitado gera intenção.
Cliente sem telefone não gera uma mensagem inválida; nunca se deduz cliente por contato.

INotificationService valida e grava a intenção na outbox em transação. FOR UPDATE na conta
serializa jobs concorrentes; índice único tenant/conta/DateOnly protege também outros
escritores e reinícios. A chave persiste independentemente de status/retries da mensagem.
No dia seguinte, conta ainda elegível recebe outra mensagem. Não modifica conta/pagamentos,
disparos manuais, orçamento ou convites. Gateway mantém envio simulado e retries existentes.

Migration 20261012191900_AddDailyReceivableReminderDeduplication adiciona campos nullable,
CHECK, FK tenant/conta com Restrict e índice único. Concede ao papel de sistema SELECT em
recebíveis/pagamentos e UPDATE(Id) para FOR UPDATE; policy WITH CHECK false rejeita
gravações financeiras. Não altera policies/guarda do papel normal. O histórico é preservado.
Foi aplicada somente em bases temporárias exclusivas. Nenhuma operação no banco de trabalho.

## Provas focadas

Quatro testes PostgreSQL rastreados @spec:AC-208 passaram, zero falhas/skips, runner exit 0.
Evidência final: .local-verification/overdue-reminders/proofs-20261012-162507/test.log e
results/overdue-postgresql.trx. Cobrem elegível/partial (saldo correto no conteúdo), repetição,
host reiniciado, dia seguinte, quitação/cancelamento, hoje/futuro, saldo zero com status legado
Pending, opt-out, cliente sem vínculo, outra conta/empresa, worker real iniciando, job limitado
ao tenant, concorrência observada com dois waiters no PostgreSQL e índice único pelo nome.
FKs rejeitam vínculo externo. Migration compara todos os campos anteriores e prova leitura/
lock administrativo, recusa de UPDATE financeiro e isolamento SQL do papel normal.

Falhas intermediárias mantidas em .local-verification/overdue-reminders/proofs-*:
compilação do teste usando método internal (prova direta usa propriedades EF), shadow
TenantId dos pagamentos na fixture, Status SQL inteiro e estado Added do pagamento novo.
A falha real do produtor foi falta de privilégio no papel administrativo: corrigida na
migration com privilégios mínimos e novas asserções. Nenhuma asserção existente removida.

Build Release final: zero avisos/erros. EF has-pending-model-changes: nenhuma diferença.
Contrato HTTP permanece igual; snapshot OpenAPI é conferido pela suíte completa.
Provas anteriores copiadas antes de renovação para .local-verification/daily-reminders/
preexisting-verification/. Cada renovação preserva TRX/JSON/log por feature e exige
engine exit 0 e record.exitCode 0. As 15 features finais passaram com ambos exit 0:
224 resultados por execução, 218 PASS, zero falhas e seis skips antigos preservados.
Evidências completas em .local-verification/daily-reminders/final/<feature>/.
Gate global: exit 0, zero erros/avisos e 95/95 critérios com prova granular; relatório
em .spec/audit-aviso-diario.json e saída bruta em .local-verification/daily-reminders/audit.raw.log.
T-105 concluída após verify; plano sequencial regenerado sem executar headless/modelos históricos.
git diff --check dos dois repositórios passou. Nenhum arquivo do frontend foi alterado nesta tarefa.

## Operação e próxima pendência

PID 27004/porta 5050 preservado. AddCustomerAutomaticReminderPreference e a migration
diária NÃO foram aplicadas a agendamento_local; ambas são necessárias antes de iniciar
a versão nova nesse banco. Segredos locais não foram alterados nem impressos.

T-023 foi revisada em .spec/revisao-t023-diario.md e continua pendente. Há divergências reais
de erros/corpos vazios e mídia dos downloads. Não encerrar tarefas históricas por aparência,
nem avançar para telas financeiro/equipe ou entrega real de e-mail nesta tarefa.
