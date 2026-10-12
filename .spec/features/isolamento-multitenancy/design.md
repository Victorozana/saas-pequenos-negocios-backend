# Design: Isolamento multi-tenancy

## Camadas de defesa

1. Middleware resolve `tenant_id` a partir da identidade autenticada.
2. Casos de uso e repositories dependem de `ITenantContext`.
3. EF Core aplica query filters a toda entidade `ITenantOwned`.
4. Interceptor de `SaveChanges` preenche ou valida o tenant antes do commit.
5. Uma transação define `app.tenant_id` localmente na conexão PostgreSQL.
6. Policies RLS filtram operações e o papel da aplicação não possui `BYPASSRLS`.

## Entidades globais e tenant-owned

- Globais: `tenants`, `users`, catálogo versionado de CNAEs e registros administrativos de sistema.
- Tenant-owned: `tenant_fiscal_profiles`, `tenant_memberships` e futuros catálogo, agenda, pedidos e consumidores.

O modelo atual possui 16 tabelas pertencentes ao tenant: `tenant_fiscal_profiles`, `tenant_memberships`, `Customers`, `ServiceItems`, `Quotations`, `QuotationItem`, `WorkOrders`, `WorkOrderItems`, `Appointments`, `ReceivableTitles`, `ReceivablePayments`, `PayableTitles`, `PayablePayments`, `NotificationMessages`, `TenantSubscriptions` e `team_invitations`. O nome físico é `tenant_id` em todas elas. Itens e pagamentos também carregam essa coluna, mesmo sem implementarem a interface de domínio; o interceptor preenche as propriedades shadow. FKs compostas impedem associar esses registros a agregados de outra empresa.

Embora o perfil fiscal use `tenant_id` também como PK, ele continua sujeito a RLS.

## Transações e pooling

O contexto PostgreSQL é configurado com escopo local (`true`) depois que a transação começa. Nenhuma configuração de sessão sobrevive ao commit/rollback. Testes alternam tenants sobre um pool pequeno para aumentar a chance de reutilização da mesma conexão física.

`TenantTransactionInterceptor` configura ROLE e contexto antes de cada comando EF. Quando o handler/EF já possui transação, o interceptor a utiliza. Quando o comando está fora de transação, cria uma transação de comando e a encerra ao consumir/dispor o reader ou terminar o comando; falhas e cancelamento fazem rollback. A configuração é feita por ADO.NET, sem recursão pelo interceptor. A ausência de tenant configura valor vazio, e as policies negam acesso.

## Operações de sistema

O cadastro público roda por uma porta administrativa específica e auditável, sem reutilizar o papel normal da aplicação com bypass. Migrações possuem credencial separada. Fluxos de suporte continuam fora do escopo.

`AdministrativeDatabaseScope` é interno aos pontos confiáveis do servidor. Cadastro, autenticação, validação de sessão, aceite de convite e processamento do outbox entram nesse escopo explicitamente. `TenantConnectionInterceptor` escolhe outra credencial antes de abrir a conexão; nunca usa input de request para selecionar credencial. O login normal não pode ser membro do papel de sistema. Ambos os papéis são restritos, sem superuser/BYPASSRLS e sem propriedade de tabelas. O papel de sistema recebe policies e DML apenas nas tabelas exigidas por essas operações; não recebe bypass global. Webhooks anônimos de billing não ganham esse acesso.

Segredos de execução: `ConnectionStrings:PostgreSql` (login normal) e `ConnectionStrings:PostgreSqlAdministrative` (login administrativo). A factory de migrations usa `ConnectionStrings:PostgreSqlMigration`, com fallback para o segredo local antigo para permitir provisionamento inicial. A API recusa credenciais proprietárias ou com bypass. A aplicação da migration e a troca de credenciais no banco de trabalho não fazem parte das provas em bases temporárias.

## Dependências

Depende das migrations de `cadastro-tenants` e da claim emitida por `identidade-acesso-inicial`.
