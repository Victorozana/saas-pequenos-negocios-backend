# Spec: Isolamento multi-tenancy

> feature: isolamento-multitenancy
> status: pronta

## Contexto

A ADR 002 exige banco PostgreSQL e esquema compartilhados com defesa em profundidade. O tenant da requisição vem exclusivamente da autenticação, é aplicado na aplicação/EF Core e também no PostgreSQL por RLS. Esta feature torna essas barreiras executáveis antes que qualquer rota de tenant seja considerada segura.

## Histórias

### US-012 — Resolver o tenant pela identidade autenticada

Como administrador, quero que a aplicação determine minha empresa pela sessão, para que nenhum request possa trocar silenciosamente meu contexto.

#### AC-028 — Requisição sem tenant autenticado não acessa dados privados

- **Dado** uma requisição sem claim válida de tenant
- **Quando** uma rota tenant-owned é solicitada
- **Então** o acesso é negado antes de consultar dados empresariais

#### AC-029 — Request não escolhe tenant

- **Dado** uma sessão válida do tenant A e um `tenant_id` do tenant B enviado em body, query ou header não confiável
- **Quando** uma rota tenant-owned é solicitada
- **Então** o valor externo é rejeitado ou ignorado e nunca substitui o tenant A da sessão

### US-013 — Impedir cruzamento na aplicação e persistência

Como dono de um tenant, quero que consultas e gravações sejam automaticamente limitadas à minha empresa, para não expor dados de concorrentes mesmo diante de erro de implementação.

#### AC-030 — Tenant A não lê dados do tenant B

- **Dado** registros equivalentes pertencentes aos tenants A e B
- **Quando** um repository consulta usando o contexto do tenant A
- **Então** somente registros do tenant A são retornados

#### AC-031 — Tenant A não altera nem remove dados do tenant B

- **Dado** um registro pertencente ao tenant B e contexto autenticado do tenant A
- **Quando** uma atualização ou remoção é tentada
- **Então** o registro do tenant B permanece inalterado e a operação não é apresentada como bem-sucedida

#### AC-032 — Divergência é rejeitada antes do commit

- **Dado** uma entidade nova ou alterada com `tenant_id` diferente do contexto atual
- **Quando** `SaveChanges` é executado
- **Então** a persistência falha antes do commit e nenhuma mudança divergente é gravada

### US-014 — Manter a barreira no PostgreSQL

Como responsável por segurança, quero RLS e privilégios restritos no banco, para que uma consulta defeituosa da aplicação continue isolada.

#### AC-033 — Papel da aplicação não contorna RLS

- **Dado** o papel PostgreSQL usado pela API e dados dos tenants A e B
- **Quando** SQL direto é executado com `app.tenant_id` definido como A
- **Então** o banco não permite ler, atualizar ou apagar linhas tenant-owned de B

#### AC-034 — Contexto não vaza pelo pool de conexões

- **Dado** duas operações sucessivas reutilizando conexões, primeiro no tenant A e depois no tenant B
- **Quando** cada transação executa consultas tenant-owned
- **Então** cada operação enxerga apenas seu próprio tenant e o contexto anterior não permanece após a transação

#### AC-035 — Toda tabela tenant-owned nasce protegida

- **Dado** uma migration que cria ou altera tabela marcada como pertencente ao tenant
- **Quando** a verificação arquitetural examina a migration e o modelo
- **Então** a tabela possui `tenant_id NOT NULL`, FK para `tenants`, índice iniciado por `tenant_id` e policy RLS

## Fora de escopo

- Fluxos de suporte, impersonação ou administração global.
- Banco ou esquema separado por tenant.
- Troca de tenant durante uma sessão.
- Identidade global de consumidores finais.

## Suposições

| ID | Suposição | Status | Resolução |
|---|---|---|---|
| ASM-007 | Nesta primeira entrega cada sessão opera em exatamente um tenant. | confirmada | A ADR define uma claim de tenant como fonte do contexto; seleção entre múltiplos vínculos fica fora de escopo. |

## Perguntas em aberto

| ID | Pergunta | Status | Resposta |
|---|---|---|---|
| Q-005 | O request pode fornecer `tenant_id` para selecionar a empresa? | respondida | Não. A ADR 002 determina resolução exclusiva pela autenticação/autorização. |
