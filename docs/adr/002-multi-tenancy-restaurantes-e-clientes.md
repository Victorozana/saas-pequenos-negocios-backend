# ADR 002 — Multi-tenancy para restaurantes e clientes dos restaurantes

- **Status:** Aceito
- **Data:** 2026-08-01
- **Decisores:** Time de Engenharia
- **Relacionados:** [PRD 002 — Fluxo de cadastro de clientes](../prd/002-fluxo-cadastro-clientes-v1.md), [RFC de stack e arquitetura](../prd/001-definicoes-iniciais-produto.md)

## Contexto

O produto atende dois públicos com papéis distintos:

1. **Restaurante**: cliente primário que contrata a plataforma e expõe seus produtos. No contexto técnico, é o **tenant**.
2. **Cliente do restaurante**: consumidor que cria pedidos e agenda entregas no contexto de um restaurante específico.

O PRD 002 define o cadastro do restaurante e estabelece que ele deve ser pessoa jurídica com CNPJ. O fluxo de cadastro do consumidor final será definido em outro PRD; portanto, esta decisão não cria requisitos de dados ou validação ainda não aprovados para esse fluxo.

O sistema precisa manter isolamento entre restaurantes, permitir escala operacional com um único banco de dados PostgreSQL e evitar que um restaurante consulte, altere ou relacione dados de outro.

## Decisão

Adotar **multi-tenancy lógico com um único banco de dados PostgreSQL e esquema compartilhado**.

Cada restaurante será representado por um registro em `tenants`. Todo dado pertencente ao restaurante carregará `tenant_id` obrigatório. O consumidor será uma entidade `customers` pertencente a um único tenant no início do produto; o mesmo indivíduo poderá existir em mais de um restaurante como registros independentes.

Não haverá, nesta fase, uma entidade global de consumidor nem compartilhamento de histórico, endereço, preferências ou dados pessoais entre restaurantes. Isso limita o escopo, reduz o risco de vazamento entre tenants e preserva autonomia futura para decidir por uma identidade global com consentimento e requisitos LGPD explícitos.

## Modelo de domínio e dados

| Conceito | Tabela inicial | Escopo | Regras arquiteturais |
| --- | --- | --- | --- |
| Restaurante / tenant | `tenants` | Global, administrativo | `id` UUID; `cnpj_normalized` obrigatório e único globalmente; criação via fluxo de onboarding com escopo de sistema. |
| Cliente do restaurante | `customers` | Tenant | `id` UUID; `tenant_id` obrigatório; nenhuma identidade é compartilhada entre tenants nesta fase. |
| Pedido, agenda, catálogo e demais recursos | Tabelas de domínio | Tenant | `tenant_id` obrigatório em toda tabela de propriedade do restaurante. |
| Usuário operador | A definir no ADR de identidade e acesso | Tenant ou sistema | Vínculo ao tenant por associação explícita; a claim de tenant é fonte para o contexto da requisição. |

Regras obrigatórias de modelagem:

- Todas as chaves primárias novas são UUIDs.
- `tenants.cnpj_normalized` armazena somente dígitos e possui índice/constraint `UNIQUE`; a validação de dígitos verificadores ocorre na aplicação antes da persistência.
- Tabelas tenant-owned possuem `tenant_id NOT NULL`, índice iniciado por `tenant_id` e `FOREIGN KEY` para `tenants(id)`.
- Relações entre recursos do mesmo tenant usam chaves compostas ou constraints que incluam `tenant_id`, evitando referência cruzada acidental. Exemplo: um `order` só pode apontar para um `customer` do mesmo tenant.
- Campos de auditoria (`created_at`, `updated_at`, `created_by`, quando aplicável) são obrigatórios na convenção de persistência.
- Regras de unicidade do consumidor, como e-mail, telefone ou documento, **não** são definidas neste ADR. Elas dependem do PRD específico de consumidores.

## Isolamento e acesso

O isolamento ocorrerá em defesa em profundidade:

1. **Resolução de tenant:** middleware resolve o `tenant_id` exclusivamente a partir da autenticação/autorização e o expõe como `ITenantContext`. O request não pode escolher o tenant via body, query string ou header não confiável.
2. **Application/Domain:** casos de uso recebem o contexto de tenant; repositories não expõem consultas sem escopo para dados tenant-owned.
3. **EF Core/Npgsql:** `DbContext` aplica filtros globais de consulta por `tenant_id`; no `SaveChanges`, um interceptor preenche ou valida o `tenant_id` e rejeita divergências.
4. **PostgreSQL RLS:** tabelas tenant-owned usam Row Level Security. Em cada transação da aplicação, `set_config('app.tenant_id', '<uuid>', true)` estabelece o contexto local; a policy permite somente linhas cujo `tenant_id` corresponde a `current_setting('app.tenant_id', true)`.
5. **Privilégios:** o papel de aplicação não possui `BYPASSRLS`. Operações de onboarding, suporte e migração usam fluxos/roles administrativos auditáveis e separados.

O contexto de tenant deve ser definido na mesma transação que executa consultas ou comandos. Isso é essencial com connection pooling: contexto de sessão não pode vazar de uma requisição para outra.

## Fluxos relevantes

### Cadastro de restaurante

1. O fluxo público recebe os dados da pessoa jurídica.
2. A aplicação normaliza e valida o CNPJ.
3. Em escopo de sistema, cria o tenant em transação curta, respeitando a unicidade global de CNPJ.
4. O tenant passa a ter contexto próprio para operações posteriores.

### Cadastro de cliente do restaurante

O comportamento de captura, validação e deduplicação dependerá do próximo PRD. Quando implementado, o caso de uso sempre criará/consultará `customers` dentro do `tenant_id` resolvido, sem pesquisa global de consumidores.

## Consequências

### Positivas

- Um único banco simplifica operação, backups, migrations, observabilidade e uso eficiente de conexões.
- RLS, constraints e filtros da aplicação oferecem múltiplas barreiras contra acesso entre restaurantes.
- A separação entre `tenants` e `customers` torna explícita a diferença entre contratante e consumidor.
- Dados do consumidor não são compartilhados por padrão, reduzindo exposição indevida.

### Custos e riscos

- Uma falha na configuração de RLS ou no contexto transacional pode bloquear consultas legítimas; testes de integração são obrigatórios.
- Operações administrativas precisam de fluxo separado e auditado.
- Um único banco estabelece um domínio de falha comum; HA, PITR, testes de restauração e limites de conexão continuam obrigatórios.
- Um consumidor recorrente em vários restaurantes poderá ter registros duplicados, decisão deliberada até existir requisito e base legal para identidade global.

## Alternativas consideradas

| Alternativa | Decisão | Motivo |
| --- | --- | --- |
| Banco por tenant | Rejeitada agora | Maior isolamento, mas custo operacional, migrations e conexões crescem proporcionalmente ao número de restaurantes. |
| Esquema por tenant | Rejeitada agora | Dificulta migrations, monitoramento e gestão de conexões sem entregar vantagem proporcional nesta fase. |
| Banco único sem RLS | Rejeitada | Depender apenas do código da aplicação não é barreira suficiente contra vazamento cross-tenant. |
| Consumidor global compartilhado | Adiada | O PRD 002 não define esse comportamento; exigiria consentimento, deduplicação e regras de privacidade próprias. |

## Critérios de aceite e verificação

- Todo teste de integração de repository prova que tenant A não lê, atualiza ou apaga registros do tenant B.
- Tentativa de gravar `tenant_id` diferente do `ITenantContext` falha antes do commit.
- Policies RLS são testadas diretamente com o papel de aplicação.
- O cadastro de restaurante rejeita CNPJ inválido e CNPJ já existente após normalização.
- Tabelas tenant-owned novas não são aceitas em migration sem `tenant_id`, índice e policy RLS.
- O primeiro PRD de consumidor final deve definir campos, consentimento, deduplicação e remoção/anonimização antes de criar endpoints de cadastro.

## Plano de evolução

1. Criar migrations de `tenants` e infraestrutura de `ITenantContext`/RLS.
2. Implementar cadastro de restaurante conforme o PRD 002 e testar duplicidade/isolamento.
3. Publicar o PRD do consumidor final e então modelar `customers` e seus fluxos.
4. Revisar a decisão se exigências regulatórias, volume, residência de dados ou isolamento contratual justificarem banco por tenant.
