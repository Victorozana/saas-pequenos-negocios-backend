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
