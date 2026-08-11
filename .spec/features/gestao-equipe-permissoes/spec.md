# Spec: Gestão de equipe e permissões

> feature: gestao-equipe-permissoes
> status: em-andamento

## Contexto

Uma empresa (tenant) precisa gerenciar sua equipe. O administrador pode enviar convites para funcionários com papéis e permissões específicas. O funcionário, ao aceitar o convite, define sua própria senha de forma segura. A API garante que o JWT revogue o acesso quase em tempo real lendo o banco a cada request protegida. A última conta administradora ativa nunca pode ser removida ou desativada.

## Histórias

### US-041 — Definir Perfil e Autorização (API e Respostas)

Como administrador do sistema, quero que as APIs de usuário e as rotas exijam e chequem permissões ativas, para garantir que as regras de negócio sejam respeitadas, retornando DTOs explícitos e bloqueando acessos indevidos (401/403).

#### AC-070 — Perfil completo do usuário

- **Dado** um usuário autenticado com permissões
- **Quando** ele solicita `/api/v1/users/me`
- **Então** o perfil retorna seus dados básicos (id, nome, email), tenant, role, permissões explícitas e status ativo.

#### AC-071 — Bloqueio de acesso por falta de permissão

- **Dado** um funcionário autenticado, mas sem a permissão `customers.write`
- **Quando** ele tenta criar ou atualizar um cliente
- **Então** o acesso é negado com HTTP 403 Forbidden e ProblemDetails.

### US-042 — Gestão e Isolamento de Membros da Equipe

Como administrador, quero listar, atualizar e desativar membros da minha equipe (tenant), para controlar o acesso ao sistema.

#### AC-072 — Isolamento multi-tenant

- **Dado** dois tenants distintos com membros próprios
- **Quando** um administrador lista a equipe (`GET /api/v1/team-members`) ou tenta atualizar um membro de outro tenant
- **Então** ele só vê membros do seu tenant e atualizações em IDs de outro tenant retornam HTTP 404 (Not Found) ou 403.

#### AC-073 — Impedir a desativação do último administrador

- **Dado** um tenant contendo apenas 1 membro ativo com papel administrador
- **Quando** esse administrador tenta desativar a própria conta ou alterar seu próprio papel
- **Então** a operação é impedida, retornando HTTP 409 Conflict, alertando que o tenant não pode ficar sem nenhum administrador ativo.

### US-043 — Convites Seguros de Funcionários

Como administrador, quero enviar convites para funcionários entrarem no sistema, para que eles definam as próprias senhas de forma autônoma.

#### AC-074 — Geração de convite único e expirável

- **Dado** o e-mail de um novo funcionário e suas permissões selecionadas
- **Quando** um convite é criado via `/api/v1/team-members/invitations`
- **Então** um token seguro é emitido, e-mail de notificação entra na outbox, e a conta fica pendente aguardando ativação.

#### AC-075 — Aceite de convite e definição de senha

- **Dado** um token válido de um funcionário recém-convidado
- **Quando** o funcionário usa o token para aceitar o convite e define sua senha
- **Então** o token é consumido, a conta é ativada e a senha gravada como hash.

## Fora de escopo

- Unidades de negócio ou multi-filiais dentro de um único tenant.
- Grupos arbitrários de permissões (além das permissões granulares explícitas aplicadas por membro).
- Dashboard visual (aqui é o escopo backend apenas).

## Suposições

| ID | Suposição | Status | Resolução |
|---|---|---|---|
| ASM-010 | A revogação imediata com JWT é feita validando os dados no banco/Contexto durante a autenticação/autorização em vez de apenas no decode. | confirmada | Sim, a validação no middleware ou auth handler fará isso. |

## Perguntas em aberto

| ID | Pergunta | Status | Resposta |
|---|---|---|---|
| Q-010 | Qual o padrão de token e expiração para os convites de equipe? | respondida | O convite expira em 7 dias (Token com uso de EmailVerificationToken existene ou similar). |
