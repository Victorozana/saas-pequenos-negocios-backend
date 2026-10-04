# Spec: Documentacao readme backend

> feature: documentacao-readme-backend
> status: pronta

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

O README ainda descreve o projeto como um esqueleto de pedidos para restaurantes,
embora a API atual seja o backend de um SaaS de gestão para pequenos prestadores
de serviço. Quem desenvolve ou integra o produto precisa de instruções atuais,
seguras e reproduzíveis para executá-lo localmente e descobrir o contrato HTTP.

## Histórias

<!-- História de usuário: quem precisa, o que precisa e por quê. -->

### US-055 — Consultar o backend atual

Como pessoa desenvolvedora do SaaS, quero um README atualizado do backend, para
que eu consiga entender seu escopo, executar a API em Development e localizar a
documentação do contrato sem recorrer ao código-fonte.

<!-- Critério de aceite: o resultado observável que um teste consegue checar.
     Escreva para GENTE: título e Então descrevem o que o usuário vê
     ("a tela avisa X"), não o detalhe técnico ("endpoint retorna 403") —
     o detalhe pode ir entre parênteses. -->

#### AC-102 — README descreve o produto e a execução local atuais

- **Dado** o repositório do backend do SaaS
- **Quando** uma pessoa lê o README
- **Então** ela encontra o escopo de gestão para pequenos negócios, os módulos
  expostos, os comandos para iniciar o perfil HTTP em `localhost:5050`, os URLs
  de saúde e OpenAPI/Swagger, e a configuração de banco sem credenciais ou
  dados pessoais de desenvolvimento.

## Fora de escopo

- Alterar endpoints, contratos OpenAPI, configuração de execução ou persistência.
- Documentar credenciais, contas seed ou segredos de desenvolvimento.

## Suposições

<!-- O que estamos ASSUMINDO sem confirmação. Status: aberta | confirmada | invalidada -->

| ID | Suposição | Status | Resolução |
|---|---|---|---|
| ASM-014 | A documentação deve refletir exclusivamente o comportamento atualmente confirmado no código e evitar detalhes de dados seed. | confirmada | Escopo solicitado pelo usuário e revisão de Program.cs, launchSettings e configurações. |

## Perguntas em aberto

<!-- O que ainda não sabemos. Status: aberta | respondida -->

| ID | Pergunta | Status | Resposta |
|---|---|---|---|
Nenhuma.
