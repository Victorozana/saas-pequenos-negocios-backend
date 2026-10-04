# Agendamento — backend do SaaS para pequenos negócios

API do SaaS de gestão para pequenos prestadores de serviço. O backend centraliza
o cadastro da empresa, clientes e serviços, orçamentos, ordens de serviço,
agenda, financeiro, equipe, notificações e assinatura. É uma aplicação
multi-tenant: as operações autenticadas usam o contexto da empresa ativa.

## Tecnologias e arquitetura

- C# e ASP.NET Core (.NET 10)
- Entity Framework Core, com PostgreSQL em produção e banco em memória como
  padrão para o perfil Development
- Autenticação Bearer JWT e autorização por permissões
- OpenAPI 3.0 e Swagger UI em Development
- Arquitetura em camadas: HTTP/Features → Application → Domain → Infrastructure

O projeto está organizado em `src/Agendamento.Api` e nas suítes de testes em
`tests/`. Os módulos HTTP ficam em `src/Agendamento.Api/Features`; casos de uso
e contratos, em `Application`; regras de negócio, em `Domain`; e persistência e
integrações, em `Infrastructure`.

## Executar localmente

Pré-requisito: SDK do .NET 10.

```powershell
dotnet restore
dotnet run --project src/Agendamento.Api --launch-profile http
```

O perfil `http` executa em Development em `http://localhost:5050`. Após iniciar,
use:

- Saúde: `GET http://localhost:5050/health`
- Swagger UI: `http://localhost:5050/swagger`
- Documento OpenAPI: `http://localhost:5050/openapi/v1.json`

Em Development, `Database:RequirePostgreSql` é `false`, portanto a API usa um
banco em memória. Para usar PostgreSQL, configure `Database__RequirePostgreSql`
como `true` e forneça `ConnectionStrings__PostgreSql` por variável de ambiente
ou pelo mecanismo seguro de segredos adotado no ambiente. Não versione
credenciais, tokens ou dados pessoais em arquivos de configuração ou exemplos.

## Módulos da API

| Área | Rotas base |
| --- | --- |
| Identidade e acesso | `/api/v1/auth`, `/api/v1/users` |
| Empresa e multi-tenancy | `/api/v1/tenants`, `/api/v1/company-registry` |
| Clientes e serviços | `/api/v1/customers`, `/api/v1/services` |
| Comercial e operação | `/api/v1/quotations`, `/api/v1/work-orders`, `/api/v1/appointments` |
| Financeiro e relatórios | `/api/v1/financial`, `/api/v1/dashboard` |
| Equipe e comunicação | `/api/v1/team-members`, `/api/v1/notifications` |
| Planos e cobrança | `/api/v1/subscriptions`, `/api/v1/webhooks/billing` |

O OpenAPI é a fonte de verdade do contrato: explore o Swagger em Development ou
consuma o documento JSON para gerar clientes tipados. Alterações em endpoints
devem manter esse contrato e seus testes atualizados.

## Testes

```powershell
dotnet test Agendamento.sln
```

As suítes cobrem regras de domínio, integração HTTP e arquitetura/contrato
OpenAPI. Para uma execução mais rápida da verificação de documentação:

```powershell
dotnet test tests/Agendamento.ArchitectureTests --filter FullyQualifiedName~ReadmeDocumentationTests
```
