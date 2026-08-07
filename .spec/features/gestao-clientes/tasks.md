# Tarefas: Gestão de Clientes

## T-026 — Modelar entidade Cliente e Value Objects [pendente]

- Refs: US-017, AC-041, AC-042
- Arquivos: `src/Agendamento.Api/Domain/Customers/Customer.cs`, `src/Agendamento.Api/Domain/Customers/CustomerDocument.cs`, `src/Agendamento.Api/Domain/Customers/CustomerAddress.cs`
- Regra: Implementar `ITenantOwned` para garantir isolamento por multi-tenancy. Validação de CPF/CNPJ e dados de contato (WhatsApp/Email).

## T-027 — Mapear persistência e Migration de Clientes [pendente]

- Refs: US-017, AC-041
- Arquivos: `src/Agendamento.Api/Infrastructure/Persistence/AgendamentoDbContext.cs`, `src/Agendamento.Api/Infrastructure/Persistence/Configurations/CustomerConfiguration.cs`
- Regra: Adicionar `DbSet<Customer>`, configurar `HasQueryFilter` por `TenantId`, chave estrangeira e índice de busca.

## T-028 — Orquestrar Casos de Uso de Clientes (Handlers) [pendente]

- Refs: US-017, US-018, US-019, AC-041, AC-043
- Arquivos: `src/Agendamento.Api/Application/Customers/CreateCustomer/`, `src/Agendamento.Api/Application/Customers/GetCustomers/`, `src/Agendamento.Api/Application/Customers/UpdateCustomer/`
- Regra: Handlers usando CQRS/MediatR ou Minimal Handlers com suporte a paginação e busca por nome/documento.

## T-029 — Expor Endpoints Minimal API de Clientes [pendente]

- Refs: US-017, US-018, US-019, AC-043, AC-044
- Arquivos: `src/Agendamento.Api/Features/Customers/CustomerEndpoints.cs`, `src/Agendamento.Api/Program.cs`
- Regra: Mapear rotas sob `/api/v1/customers` protegidas por `RequireAuthorization()`. Documentar OpenAPI/Swagger com tags e responses.

## T-030 — Testes de Integração e Isolamento de Clientes [pendente]

- Refs: AC-041, AC-044
- Arquivos: `tests/Agendamento.IntegrationTests/Customers/CustomerApiTests.cs`
- Regra: Garantir que um tenant A não consegue visualizar, alterar ou listar clientes do tenant B.
