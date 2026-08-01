# Spec: Identidade e acesso inicial

> feature: identidade-acesso-inicial
> status: rascunho

## Contexto

Uma empresa não se autentica. O painel é acessado por uma pessoa física registrada globalmente em `users`, vinculada explicitamente ao tenant. Esta feature define a identidade mínima necessária para que o cadastro composto crie o primeiro administrador, confirme seu e-mail e somente então permita login.

## Histórias

### US-004 — Registrar a identidade do primeiro administrador

Como pessoa responsável pela empresa, quero possuir minha própria identidade de acesso, para administrar o tenant sem compartilhar credenciais empresariais.

#### AC-007 — CPF e e-mail identificam uma única pessoa

- **Dado** um usuário já registrado com determinado CPF ou e-mail após normalização
- **Quando** outro cadastro tenta criar uma nova identidade com o mesmo CPF ou e-mail
- **Então** nenhuma identidade duplicada é criada e o cadastro recebe um conflito sem expor os dados existentes

#### AC-008 — Senhas nunca são recuperáveis

- **Dado** uma senha válida enviada no cadastro
- **Quando** o usuário é persistido
- **Então** apenas um hash produzido pelo mecanismo de identidade é armazenado e a senha não aparece em respostas ou logs

### US-005 — Confirmar a posse do e-mail

Como primeiro administrador, quero confirmar meu e-mail, para que terceiros não ativem uma conta usando meu endereço.

#### AC-009 — Cadastro envia verificação sem ativar o login

- **Dado** um tenant e seu primeiro administrador recém-criados
- **Quando** a transação de cadastro é concluída
- **Então** o usuário permanece pendente e uma solicitação de envio de verificação é registrada para seu e-mail

#### AC-010 — Token válido confirma o e-mail uma única vez

- **Dado** um token de verificação válido, não utilizado e dentro da validade
- **Quando** `POST /api/v1/auth/email-verifications` é solicitado com o token
- **Então** o e-mail é marcado como verificado e uma repetição não altera novamente o estado

#### AC-011 — Token inválido ou vencido não ativa a conta

- **Dado** um token inexistente, adulterado, já utilizado ou vencido
- **Quando** a confirmação é solicitada
- **Então** a conta continua pendente e a resposta informa que a verificação não pôde ser concluída sem revelar dados pessoais

### US-006 — Entrar no painel do tenant

Como administrador com e-mail confirmado, quero fazer login, para receber um contexto autenticado limitado às empresas às quais pertenço.

#### AC-012 — E-mail não confirmado impede login

- **Dado** credenciais corretas de um usuário cujo e-mail ainda não foi confirmado
- **Quando** `POST /api/v1/auth/sessions` é solicitado
- **Então** nenhuma sessão é criada e a resposta orienta a confirmar o e-mail

#### AC-013 — Login válido carrega identidade e vínculo

- **Dado** credenciais corretas de um usuário ativo e verificado vinculado a um tenant ativo
- **Quando** o login é solicitado
- **Então** uma sessão de curta duração é emitida com identificadores de usuário, tenant e papel, sem CPF ou outros dados desnecessários

## Fora de escopo

- Recuperação ou alteração de senha.
- Autenticação multifator, login social ou SSO.
- Refresh tokens e revogação distribuída.
- Convites e administração de funcionários adicionais.
- KYC, RG/CNH, biometria ou prova de vida.

## Suposições

| ID | Suposição | Status | Resolução |
|---|---|---|---|
| ASM-002 | A sessão inicial será um JWT bearer de curta duração, sem refresh token. | confirmada | Confirmada pelo usuário em 2026-08-01. |
| ASM-003 | Senhas terão no mínimo 12 caracteres e o token de e-mail será de uso único, válido por 24 horas. | confirmada | Confirmada pelo usuário em 2026-08-01. |

## Perguntas em aberto

| ID | Pergunta | Status | Resposta |
|---|---|---|---|
| Q-002 | O primeiro administrador precisa ser sempre o representante legal? | respondida | Não. Pode ser representante legal ou funcionário autorizado; o vínculo registra `is_legal_representative`. |
