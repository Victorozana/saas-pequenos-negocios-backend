# Design: Identidade e acesso inicial

## Modelo

`users` é global e representa uma pessoa física. A relação com empresas não fica em `users`; ela será persistida por `tenant_memberships` na feature de cadastro de tenants. Isso permite que a mesma pessoa administre mais de um tenant no futuro.

## Segurança de credenciais

O domínio não conhece hash, JWT ou ASP.NET Identity. A aplicação depende de portas para hash/verificação e emissão de sessão; a infraestrutura implementa essas portas. CPF e e-mail são normalizados antes da consulta e protegidos por constraints únicas.

## Verificação de e-mail

O cadastro grava um pedido de envio em uma caixa de saída na mesma transação dos dados empresariais. O token entregue ao usuário é aleatório; o banco armazena somente seu digest. A confirmação é um comando HTTP `POST`, pois altera estado.

## Sessão

A sessão inclui apenas `sub`, `tenant_id`, papel e metadados técnicos mínimos. O login consulta vínculos ativos e rejeita usuário não verificado, suspenso ou vinculado somente a tenant inativo.

## Dependências

Depende de `fundacao-api-testes`. A criação do usuário inicial é orquestrada por `cadastro-tenants`; esta feature fornece os componentes de identidade.
