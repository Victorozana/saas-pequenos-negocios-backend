# PostgreSQL RLS — implementação e operação local

A migration `20261012041401_EnforceTenantRowLevelSecurity` roda após todas as tabelas atuais. Ela renomeia as colunas físicas para `tenant_id`, adiciona/backfilla a coluna nos itens e pagamentos, instala FKs/índices e habilita ENABLE/FORCE RLS nas 16 tabelas pertencentes ao tenant. A fonte histórica `AddRowLevelSecurity` não foi ativada: seus nomes e sua posição na cadeia estavam incorretos.

Os papéis `agendamento_app_user` e `agendamento_system_user` não possuem LOGIN, superuser nem BYPASSRLS, e não devem ser proprietários de tabelas. Logins distintos recebem exclusivamente o papel correspondente. O login normal não deve receber membership de sistema, CREATE no schema ou DDL; não recebe privilégios de migration. A API verifica essas restrições na abertura das conexões.

## Credenciais separadas

Configurar somente nos segredos locais do .NET ou no gerenciador de segredos do ambiente:

- `ConnectionStrings:PostgreSqlMigration`: credencial de provisionamento/migrations, nunca usada pela API. Para compatibilidade com o provisionamento inicial, a factory aceita o segredo PostgreSql antigo quando essa chave ainda não existir.
- `ConnectionStrings:PostgreSql`: login restrito com membership somente em `agendamento_app_user`.
- `ConnectionStrings:PostgreSqlAdministrative`: outro login restrito com membership somente em `agendamento_system_user`.

As senhas devem ser geradas e gravadas por canal protegido. Não colocar conexões ou senhas em arquivos rastreados, relatórios ou argumentos exibidos no terminal. As provas geram logins e senhas descartáveis exclusivamente para suas bases temporárias, sem alterar estes segredos.

## Aplicação local posterior

Esta retomada **não aplicou a migration em `agendamento_local`**, não alterou cadastros e não mudou credenciais locais. Para usar a API com esta implementação, o banco precisa da migration e dos dois logins restritos; o segredo antigo com papel proprietário/superuser passa a ser recusado pela API.

A ordem de provisionamento é: preservar a credencial atual como segredo de migration; revisar os dados antigos e validar a migration numa cópia temporária; aplicar a migration com a credencial de provisionamento; criar os logins restritos e conceder seus respectivos papéis; configurar os dois segredos de execução; iniciar a API. Não usar a credencial de migration como credencial administrativa da API.

O backfill deriva o tenant dos agregados sem remover registros. Órfãos, referências históricas entre empresas ou tenants inexistentes causam falha transacional das novas FKs; os testes não certificam que o banco de trabalho esteja livre dessas inconsistências. Corrigir dados exige revisão específica, sem descarte automático.

## Provas

`PostgreSqlRlsTests` cria bases exclusivas, aplica a cadeia antiga, semeia duas empresas em todas as tabelas e aplica a migration nova. Examina catálogo real, RLS, NOT NULL, FKs e índices; testa SQL direto SELECT/INSERT/UPDATE/DELETE, rejeição de referência entre tenants e credencial proprietária. Usa logins sem superuser/BYPASSRLS e pool de uma conexão, verificando o mesmo PID após commit/rollback.

`CrossTenantIsolationTests` testa cadastro público, autenticação e isolamento HTTP/EF no PostgreSQL com credenciais separadas. `TeamFinancialPostgreSqlTests` testa aprovação concorrente, pagamentos, convite público e replay com os mesmos interceptors da API. `TenantOwnedMigrationTests` examina o modelo, dependentes e SQL da migration descoberta, sem conexão ao banco.

Os seis testes antigos pulados continuam pulados e não contam como prova. O webhook billing anônimo não recebeu porta administrativa; integração de gateway continua fora desta etapa. A auditoria global permanece independente do sucesso destas provas.

O pool de uma conexão é exclusivo da prova de reutilização. As outras provas usam pool maior. No teste financeiro, uma transação de preparação segura a linha do orçamento até que pg_stat_activity mostre duas consultas FOR UPDATE bloqueadas simultaneamente. Só então a preparação libera a linha; ambas as aprovações devem retornar sucesso e os recebíveis devem continuar sendo exatamente entrada e saldo. Essa asserção falha se as requisições forem serializadas pelo pool.

## Histórico de notificações — migration adicional

A migration 20261012154514_AddNotificationCustomerHistory adiciona CustomerId e Audience à
outbox existente, com FK composta para Customers(tenant_id, Id), CHECK de classificação e
Restrict na exclusão. As policies/grants RLS existentes continuam protegendo NotificationMessages.
As linhas antigas recebem LegacyUnlinked sem alteração dos campos históricos e sem dedução
por telefone/e-mail. Não são apresentadas como histórico comprovado de cliente.

NotificationHistoryPostgreSqlTests valida upgrade a partir da migration RLS em duas bases
exclusivas, preservação integral do histórico, vínculo externo rejeitado e despacho/histórico
com credenciais restritas separadas. Nenhuma dessas migrations foi aplicada a agendamento_local
nesta etapa. A operação local continua exigindo o provisionamento documentado acima.

## Provisionamento local concluído — 12/10/2026

Depois dos testes temporários descritos acima, o usuário pediu corrigir a falha de inicialização
do perfil http. A compilação Debug/Release e o build frontend passavam; o crash era no
DbSeeder ao abrir a porta administrativa, sem ConnectionStrings:PostgreSqlAdministrative.

Foi gerado backup custom de agendamento_local em .local-verification/runtime-repair/
agendamento_local-before-rls.dump e restaurado em cópia exclusiva. As duas migrations pendentes
(EnforceTenantRowLevelSecurity e AddNotificationCustomerHistory) foram validadas na cópia,
comparando contagens/hashes de todos os valores originais das 21 tabelas, antes/depois do upgrade.
Só então foram aplicadas a agendamento_local. A mesma comparação passou no banco local.

Foram provisionados dois logins restritos distintos, sem propriedade de tabelas, superuser,
BYPASSRLS, CREATEDB ou CREATEROLE, com membership exclusivo no papel app ou system. As conexões
foram gravadas nos segredos .NET por stdin; a credencial anterior foi preservada em
PostgreSqlMigration. Nenhuma senha/conexão foi exposta nem gravada em arquivo rastreado.

O comando dotnet run --project .\src\Agendamento.Api --launch-profile http voltou a iniciar;
/health e /openapi/v1.json retornaram 200. A consulta EF usando login normal restrito e contexto
de tenant também passou. Os cadastros permaneceram intactos. O worker processou as mensagens
pendentes pelo mock existente, atualizando a outbox conforme o fluxo normal; não houve envio
real. A cópia temporária foi removida; o backup foi mantido.

A execução oculta de validação é encerrada ao concluir para liberar 5050 para o terminal do
usuário. Os avisos existentes de ValueComparer não foram a causa deste crash. A intermitência
histórica do teste de build segue sendo problema separado, sem causa definitiva identificada.

scripts/configurar-banco-local.ps1 agora grava somente PostgreSqlMigration e preserva as duas
credenciais de runtime. O script não deve voltar a substituir o login normal pela credencial
de provisionamento nem anunciar que uma única conexão é suficiente para iniciar a API.

## Preferência de lembretes — 12/10/2026

A migration `20261012174238_AddCustomerAutomaticReminderPreference` acrescenta boolean
obrigatório em Customers, habilitando os clientes existentes sem alterar seus campos anteriores.
Foi validada somente em base temporária exclusiva, comparando os valores antigos e exercitando
persistência sob login restrito/RLS. Não foi aplicada a agendamento_local nesta retomada.
A API nova exige essa coluna; a instância preexistente PID 27004/porta 5050 foi preservada.
O provisionamento RLS/histórico concluído acima continua válido; esta migration é adicional.

## Aviso diário de contas vencidas — 12/10/2026

`20261012191900_AddDailyReceivableReminderDeduplication` acrescenta ReceivableTitleId e
ReminderDate nullable à outbox, índice único por tenant/conta/dia, CHECK do par de campos
e FK composta tenant/conta com Restrict. O histórico anterior permanece com ambos null.
O worker usa a porta administrativa restrita existente. A migration concede SELECT apenas
nos recebíveis/pagamentos e UPDATE(Id) para permitir FOR UPDATE, com policy que rejeita
gravações financeiras (WITH CHECK false). Policies novas são exclusivas do papel de sistema;
as policies/grants e a guarda restritiva do login normal permanecem.

Esta migration e AddCustomerAutomaticReminderPreference são necessárias antes de iniciar
a versão nova contra agendamento_local. Nenhuma delas foi aplicada ao banco de trabalho
nesta tarefa. A API preexistente PID 27004/porta 5050 é preservada; não reiniciar essa API
com a versão nova enquanto o rollout dessas migrations estiver pendente.
