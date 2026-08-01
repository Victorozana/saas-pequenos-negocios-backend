# Agendamento

Esqueleto inicial do sistema de agendamento de pedidos para restaurantes.

## Decisões fechadas

- Backend: C# + ASP.NET Core (.NET 10)
- Persistência: PostgreSQL + EF Core/Npgsql
- Cache e rate limiting distribuído: Redis + StackExchange.Redis
- Mensageria: RabbitMQ + MassTransit, com padrão transactional outbox
- Arquitetura: monólito modular, camadas HTTP → Application → Domain → Infrastructure e workers separados

## Estrutura

- `src/Agendamento.Api/Application`: casos de uso e contratos de portas.
- `src/Agendamento.Api/Domain`: entidades, regras e eventos de domínio.
- `src/Agendamento.Api/Infrastructure`: persistência e integrações externas.
- `src/Agendamento.Api/Features`: controllers/endpoints e DTOs por módulo.

O único endpoint do esqueleto é `GET /health`. As integrações externas ainda não são registradas para manter o projeto inicial vazio e compilável sem dependências de terceiros.
