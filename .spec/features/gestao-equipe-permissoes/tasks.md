# Tasks: Gestao equipe permissoes

> feature: gestao-equipe-permissoes

<!--
  Como ler este arquivo (o formato é verificado por `onp-spec audit`):
  - T-xxx = tarefa (código de rastreio, único no projeto inteiro).
  - Toda tarefa referencia em `Refs:` pelo menos uma história de usuário
    (US-xxx) ou critério de aceite (AC-xxx).
  - Toda tarefa lista os arquivos que cria/altera em `Arquivos:` — capriche:
    é o que decide o que `onp-spec plano` roda em PARALELO (arquivos
    disjuntos) e o que roda em sequência.
  - Campos opcionais por tarefa, usados pelo plano de execução:
    `- Modelo: claude-sonnet-5` e `- Esforço: alto` (baixo|medio|alto|xalto|max).
# Tasks: Gestao equipe permissoes

> feature: gestao-equipe-permissoes

<!--
  Como ler este arquivo (o formato é verificado por `onp-spec audit`):
  - T-xxx = tarefa (código de rastreio, único no projeto inteiro).
  - Toda tarefa referencia em `Refs:` pelo menos uma história de usuário
    (US-xxx) ou critério de aceite (AC-xxx).
  - Toda tarefa lista os arquivos que cria/altera em `Arquivos:` — capriche:
    é o que decide o que `onp-spec plano` roda em PARALELO (arquivos
    disjuntos) e o que roda em sequência.
  - Campos opcionais por tarefa, usados pelo plano de execução:
    `- Modelo: claude-sonnet-5` e `- Esforço: alto` (baixo|medio|alto|xalto|max).
  - Uma tarefa só pode virar [concluida] quando os critérios de aceite dela
    tiverem prova PASS registrada por `onp-spec verify`.
  Status: pendente | em-andamento | concluida
    (atalho: `onp-spec tarefa <feature> <T-xxx> <status>`)
-->

- `[concluida]` T-080 Atualizar entidade User com nome e DTO Profile (Refs: AC-070) Arquivos: src/Agendamento.Api/Domain/Identity/User.cs, src/Agendamento.Api/Application/Tenants/RegisterTenant/RegisterTenantHandler.cs, src/Agendamento.Api/Features/Identity/CurrentUserEndpoints.cs, src/Agendamento.Api/Application/Identity/GetUserProfile/UserProfileResponse.cs
- `[concluida]` T-081 Definir Permissions e autorização em TenantMembership (Refs: AC-070, AC-071) Arquivos: src/Agendamento.Api/Domain/Identity/Permissions.cs, src/Agendamento.Api/Domain/Tenants/TenantMembership.cs, src/Agendamento.Api/Infrastructure/Persistence/Configurations/TenantMembershipConfiguration.cs
- `[concluida]` T-082 Middleware/Handler de Permissões (Refs: AC-071) Arquivos: src/Agendamento.Api/Infrastructure/Authorization/PermissionAuthorizationExtensions.cs, src/Agendamento.Api/Infrastructure/DependencyInjection.cs
- `[concluida]` T-083 Aplicar RequirePermission nos endpoints (Refs: AC-071) Arquivos: src/Agendamento.Api/Features/Customers/CustomerEndpoints.cs, src/Agendamento.Api/Features/Services/ServiceEndpoints.cs, src/Agendamento.Api/Features/Quotations/QuotationEndpoints.cs, src/Agendamento.Api/Features/Appointments/AppointmentEndpoints.cs, src/Agendamento.Api/Features/Dashboard/DashboardEndpoints.cs, src/Agendamento.Api/Features/Financial/FinancialEndpoints.cs
- `[concluida]` T-084 Endpoints de gestão de equipe listar/status (Refs: AC-072, AC-073) Arquivos: src/Agendamento.Api/Features/Team/TeamMemberEndpoints.cs, src/Agendamento.Api/Program.cs
- `[concluida]` T-085 Envio de convite e aceite (Refs: AC-074, AC-075) Arquivos: src/Agendamento.Api/Features/Team/TeamMemberEndpoints.cs, src/Agendamento.Api/Domain/Tenants/TeamInvitation.cs, src/Agendamento.Api/Infrastructure/Persistence/Configurations/TeamInvitationConfiguration.cs
- `[concluida]` T-086 Serialização de Enums e CORS (Refs: AC-070) Arquivos: src/Agendamento.Api/Program.cs, src/Agendamento.Api/Infrastructure/DependencyInjection.cs
- `[concluida]` T-087 Configuração e validação de testes OpenAPI (Refs: AC-070) Arquivos: tests/Agendamento.IntegrationTests/Features/TeamMemberTests.cs
