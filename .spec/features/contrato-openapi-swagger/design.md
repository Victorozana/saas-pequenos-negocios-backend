# Design: Contrato OpenAPI e Swagger

## Fonte do contrato

Metadados dos Minimal APIs geram OpenAPI v1. Transformers compartilhados adicionam `application/problem+json`, esquema bearer e convenções comuns. DTOs explícitos impedem que entidades de persistência vazem para o contrato.

## Rotas previstas ao fim desta entrega

| Método | Rota | Acesso |
|---|---|---|
| GET | `/health` | Público |
| GET | `/api/v1/company-registry/cnpj/{cnpj}` | Público com rate limit |
| POST | `/api/v1/tenants` | Público com rate limit e idempotência |
| POST | `/api/v1/auth/email-verifications` | Público com rate limit |
| POST | `/api/v1/auth/sessions` | Público com rate limit |
| GET | `/api/v1/tenants/me` | Bearer autenticado |
| GET | `/api/v1/users/me` | Bearer autenticado |
| GET | `/openapi/v1.json` | Documento técnico |

## Verificação

Um teste inicia a aplicação, obtém o documento gerado, normaliza ordenação e compara semanticamente com `docs/openapi/v1.json`. Outro enumera endpoints e prova que cada método/rota está documentado. Uma lista de nomes sensíveis protege schemas de resposta e exemplos.

## Evolução

Qualquer nova rota deverá trazer seus critérios de aceite, testes funcionais, metadados OpenAPI e atualização do contrato versionado na mesma tarefa.

## Dependências

É a última feature estrutural da entrega e documenta as rotas implementadas pelas features de identidade e cadastro.
