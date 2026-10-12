# Design: Contrato OpenAPI e Swagger

## Fonte do contrato

Metadados dos Minimal APIs geram OpenAPI v1. Erros de domínio/validação usam Results.Problem explicitamente; o transformer não inventa mídia para erros vazios. JWT mantém 401 sem corpo, com WWW-Authenticate e sem detalhes internos; erros de permissão são 403 ProblemDetails. Falhas do binding 400/415 recebem corpo seguro por StatusCodePages dedicado, mantendo 204/404/401 intencionais vazios. DTOs explícitos tipam respostas JSON.

Exports declaram application/pdf como string binary em OpenAPI 3.0 e text/csv como string textual, com Content-Disposition e Content-Length. Provas HTTP verificam bytes, conteúdo, nome, mídia e isolamento, além de comparar a metadata gerada.

## Rotas previstas ao fim desta entrega

| Método | Rota | Acesso |
|---|---|---|
| GET | `/health` | Público |
| GET | `/api/v1/company-registry/{cnpj}` | Público com rate limit |
| POST | `/api/v1/tenants` | Público com idempotência |
| POST | `/api/v1/auth/email-verifications` | Público |
| POST | `/api/v1/auth/sessions` | Público |
| GET | `/api/v1/tenants/me` | Bearer autenticado |
| GET | `/api/v1/users/me` | Bearer autenticado |
| GET | `/openapi/v1.json` | Documento técnico |

## Verificação

Um teste inicia a aplicação, obtém o documento gerado, normaliza ordenação e compara semanticamente com `docs/openapi/v1.json`. Outro enumera endpoints e prova que cada método/rota está documentado. A inspeção estrutural dos usos de schemas e exemplos protege dados sensíveis; testes HTTP adicionais cobrem erros, respostas vazias e downloads.

## Evolução

Qualquer nova rota deverá trazer seus critérios de aceite, testes funcionais, metadados OpenAPI e atualização do contrato versionado na mesma tarefa.

## Dependências

É a última feature estrutural da entrega e documenta as rotas implementadas pelas features de identidade e cadastro.
