# Spec: Onboarding token desenvolvimento

> feature: onboarding-token-desenvolvimento
> status: rascunho

<!--
  Como ler este arquivo (o formato é verificado por `onp-spec audit`):
  - US-xxx = história de usuário · AC-xxx = critério de aceite
    ASM-xxx = suposição · Q-xxx = pergunta em aberto
    São códigos de rastreio: ligam a especificação às tarefas e aos testes.
  - Toda história de usuário precisa de pelo menos um critério de aceite.
  - Todo critério de aceite precisa de Dado/Quando/Então completos.
  - Os códigos são únicos no projeto inteiro (nunca reutilize um número).
  - Suposições e Perguntas em aberto são OBRIGATÓRIAS: se não há nenhuma,
    escreva "Nenhuma." — mas desconfie: quase toda feature esconde uma.
-->

## Contexto

O frontend de onboarding precisa obter, apenas em Development ou Test, o token necessário para confirmar o e-mail recém-cadastrado. O contrato OpenAPI deve informar esse header sem declarar o token como dado de produção ou resposta pública.

## Histórias

<!-- História de usuário: quem precisa, o que precisa e por quê. -->

### US-053 — Consumir a confirmação de e-mail em desenvolvimento

Como pessoa desenvolvedora do frontend, quero encontrar no OpenAPI o header de token de desenvolvimento do cadastro, para implementar a confirmação de e-mail sem supor um contrato não documentado.

<!-- Critério de aceite: o resultado observável que um teste consegue checar.
     Escreva para GENTE: título e Então descrevem o que o usuário vê
     ("a tela avisa X"), não o detalhe técnico ("endpoint retorna 403") —
     o detalhe pode ir entre parênteses. -->

#### AC-100 — Header de verificação é documentado somente em ambientes não produtivos

- **Dado** a API executada em Development ou Test
- **Quando** o contrato de `POST /api/v1/tenants` é consultado
- **Então** a resposta HTTP 201 descreve o header `X-Development-Verification-Token` como uma string disponível somente para desenvolvimento, e `RegisterTenantResponse` não declara token bruto.

## Fora de escopo

- Alterar a geração, a persistência ou o consumo do token.
- Expor token de verificação em Production.
- Alterar endpoints ou o frontend.

## Suposições

<!-- O que estamos ASSUMINDO sem confirmação. Status: aberta | confirmada | invalidada -->

| ID | Suposição | Status | Resolução |
|---|---|---|---|
| ASM-013 | O OpenAPI de Development/Test pode documentar um header destinado exclusivamente ao fluxo local. | confirmada | O endpoint já envia o header somente quando o ambiente é Development; Test é incluído para tornar o contrato verificável. |

## Perguntas em aberto

<!-- O que ainda não sabemos. Status: aberta | respondida -->

| ID | Pergunta | Status | Resposta |
|---|---|---|---|
| Nenhuma. | — | respondida | O usuário autorizou a alteração do backend e definiu que o token deve permanecer exclusivo de Development. |
