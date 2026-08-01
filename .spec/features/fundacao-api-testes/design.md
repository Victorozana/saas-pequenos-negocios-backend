# Design: Fundação da API e dos testes

## Direção

A solução continuará como monólito modular. A fundação adicionará projetos de teste sem criar projetos de produção por camada antes de existir código suficiente para justificar essa divisão.

## Estratégia de testes

- Testes unitários: domínio e aplicação, sem infraestrutura.
- Testes de integração: host HTTP real com dependências substituíveis e PostgreSQL em container.
- Testes arquiteturais: dependências entre namespaces/projetos, ausência de segredos e invariantes estruturais.
- Todos os critérios de aceite terão ao menos um teste cujo título contenha `@spec:AC-xxx`.

## Persistência

EF Core e Npgsql serão registrados pela infraestrutura. Migrations ficarão em `src/Agendamento.Api/Infrastructure/Persistence/Migrations` enquanto houver um único projeto de produção. O banco de cada suíte será descartável e terá nome aleatório.

## Configuração

Arquivos versionados conterão apenas chaves e valores seguros. Segredos virão de variáveis de ambiente, user-secrets no desenvolvimento ou mecanismo equivalente no deploy. A validação ocorrerá no startup.

## Dependências

Esta feature é pré-requisito de todas as demais specs desta entrega.
