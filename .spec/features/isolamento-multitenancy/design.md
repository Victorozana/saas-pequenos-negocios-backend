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

Embora o perfil fiscal use `tenant_id` também como PK, ele continua sujeito a RLS.

## Transações e pooling

O contexto PostgreSQL é configurado com escopo local (`true`) depois que a transação começa. Nenhuma configuração de sessão sobrevive ao commit/rollback. Testes alternam tenants sobre um pool pequeno para aumentar a chance de reutilização da mesma conexão física.

## Operações de sistema

O cadastro público roda por uma porta administrativa específica e auditável, sem reutilizar o papel normal da aplicação com bypass. Migrações possuem credencial separada. Fluxos de suporte continuam fora do escopo.

## Dependências

Depende das migrations de `cadastro-tenants` e da claim emitida por `identidade-acesso-inicial`.
