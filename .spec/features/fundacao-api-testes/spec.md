# Spec: Fundação da API e dos testes

> feature: fundacao-api-testes
> status: pronta

## Contexto

O projeto contém apenas uma API ASP.NET Core .NET 10 com `GET /health`, sem persistência, projetos de teste ou dependências externas. Esta feature estabelece a base mínima verificável para que identidade, cadastro de tenants, PostgreSQL, RLS e OpenAPI sejam desenvolvidos por TDD sem misturar configuração com regras de negócio.

## Histórias

### US-001 — Desenvolver sobre uma base reproduzível

Como pessoa desenvolvedora, quero restaurar e compilar a solução de forma determinística, para que falhas de configuração apareçam antes das regras de negócio.

#### AC-001 — A solução compila sem avisos

- **Dado** um checkout limpo com o SDK .NET 10 e as dependências restauradas
- **Quando** a solução é compilada em configuração Release
- **Então** todos os projetos compilam sem erros nem avisos

#### AC-002 — A API continua informando sua saúde

- **Dado** a API iniciada no ambiente de testes
- **Quando** `GET /health` é solicitado
- **Então** a resposta é HTTP 200 e informa que a aplicação está saudável

### US-002 — Provar comportamento com testes automatizados

Como pessoa desenvolvedora, quero projetos separados de testes unitários, de integração HTTP e de integração PostgreSQL, para que cada risco seja validado na camada adequada.

#### AC-003 — O comando oficial executa todos os testes

- **Dado** testes unitários e de integração anotados com seus critérios de aceite
- **Quando** o comando oficial de testes da solução é executado
- **Então** todos os projetos de teste são descobertos e qualquer falha produz código de saída diferente de zero

#### AC-004 — Integrações usam PostgreSQL real e isolado

- **Dado** uma execução dos testes de persistência
- **Quando** o banco de testes é provisionado
- **Então** ele usa PostgreSQL compatível com produção, aplica migrations e não compartilha dados com outra execução

### US-003 — Configurar persistência sem expor segredos

Como pessoa operadora, quero configuração externa e validação no início da aplicação, para que credenciais não sejam versionadas nem erros de conexão permaneçam ocultos.

#### AC-005 — Segredos não possuem valor real no repositório

- **Dado** os arquivos versionados de configuração da aplicação
- **Quando** eles são inspecionados
- **Então** não existe senha, token ou string de conexão real armazenada no código-fonte

#### AC-006 — Configuração inválida falha de forma explícita

- **Dado** um ambiente que exige PostgreSQL e não fornece uma string de conexão válida
- **Quando** a aplicação tenta iniciar
- **Então** a inicialização falha com mensagem de configuração sem revelar credenciais

## Fora de escopo

- Modelar tenants, usuários, vínculos ou perfis fiscais.
- Escolher provedores de e-mail, consulta de CNPJ, pagamentos ou emissão fiscal.
- Criar Redis, RabbitMQ, outbox ou observabilidade completa.
- Implementar regras de autenticação ou autorização.

## Suposições

| ID | Suposição | Status | Resolução |
|---|---|---|---|
| ASM-001 | O PostgreSQL real poderá ser provisionado por Testcontainers durante testes de integração. | confirmada | Compatível com a decisão PostgreSQL da RFC 001 e com a exigência da ADR 002 de testar RLS diretamente. |

## Perguntas em aberto

| ID | Pergunta | Status | Resposta |
|---|---|---|---|
| Q-001 | A fundação deve incluir configurações de Redis e RabbitMQ? | respondida | Não nesta entrega; a primeira feature não utiliza esses componentes. |
