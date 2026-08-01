# Tasks: Identidade e acesso inicial

> feature: identidade-acesso-inicial

## T-005 — Modelar usuário e credenciais [concluida]
- Refs: US-004, AC-007, AC-008
- Arquivos: src/Agendamento.Api/Domain/Identity/User.cs, src/Agendamento.Api/Domain/Identity/UserStatus.cs, src/Agendamento.Api/Application/Identity/IPasswordService.cs, src/Agendamento.Api/Infrastructure/Identity/AspNetPasswordService.cs, src/Agendamento.Api/Infrastructure/Persistence/Configurations/UserConfiguration.cs, tests/Agendamento.UnitTests/Identity/UserTests.cs, tests/Agendamento.IntegrationTests/Identity/UserPersistenceTests.cs
- Notas: CPF e e-mail normalizados possuem unicidade global; usar o password hasher da stack escolhida.

## T-006 — Persistir tokens de confirmação de e-mail [concluida]
- Refs: US-005, AC-009, AC-010, AC-011
- Arquivos: src/Agendamento.Api/Domain/Identity/EmailVerificationToken.cs, src/Agendamento.Api/Application/Identity/VerifyEmail/VerifyEmailCommand.cs, src/Agendamento.Api/Application/Identity/VerifyEmail/VerifyEmailHandler.cs, src/Agendamento.Api/Application/Identity/VerifyEmail/VerifyEmailResult.cs, src/Agendamento.Api/Application/Identity/VerifyEmail/IClock.cs, src/Agendamento.Api/Application/Identity/VerifyEmail/IEmailVerificationTokenRepository.cs, src/Agendamento.Api/Application/Identity/VerifyEmail/IEmailVerificationUser.cs, src/Agendamento.Api/Application/Identity/VerifyEmail/IEmailVerificationUserRepository.cs, src/Agendamento.Api/Infrastructure/Persistence/Configurations/EmailVerificationTokenConfiguration.cs, tests/Agendamento.UnitTests/Identity/EmailVerificationTokenTests.cs
- Notas: Persistir somente digest do token, expiração e consumo; o envio real será abstraído.

## T-007 — Criar porta de envio e caixa de saída de e-mail [concluida]
- Refs: US-005, AC-009
- Arquivos: src/Agendamento.Api/Application/Identity/IEmailVerificationSender.cs, src/Agendamento.Api/Infrastructure/Email/IEmailOutbox.cs, src/Agendamento.Api/Infrastructure/Email/OutboxEmailVerificationSender.cs, src/Agendamento.Api/Infrastructure/Persistence/Entities/EmailOutboxMessage.cs, tests/Agendamento.IntegrationTests/Identity/EmailVerificationOutboxTests.cs
- Notas: O commit do cadastro e o pedido de envio devem ser atômicos; provedor externo fica fora do escopo.

## T-008 — Expor confirmação de e-mail [concluida]
- Refs: US-005, AC-010, AC-011
- Arquivos: src/Agendamento.Api/Features/Identity/EmailVerificationEndpoints.cs, src/Agendamento.Api/Features/Identity/VerifyEmailRequest.cs, tests/Agendamento.IntegrationTests/Identity/EmailVerificationEndpointTests.cs
- Notas: Implementar `POST /api/v1/auth/email-verifications` e respostas `application/problem+json`.

## T-009 — Autenticar e emitir sessão [concluida]
- Refs: US-006, AC-012, AC-013
- Arquivos: src/Agendamento.Api/Application/DependencyInjection.cs, src/Agendamento.Api/Infrastructure/DependencyInjection.cs, src/Agendamento.Api/Program.cs, src/Agendamento.Api/Application/Identity/CreateSession/CreateSessionCommand.cs, src/Agendamento.Api/Application/Identity/CreateSession/CreateSessionHandler.cs, src/Agendamento.Api/Application/Identity/CreateSession/CreateSessionResult.cs, src/Agendamento.Api/Application/Identity/CreateSession/IIdentityAuthenticationStore.cs, src/Agendamento.Api/Application/Identity/CreateSession/SessionPrincipal.cs, src/Agendamento.Api/Application/Identity/ISessionIssuer.cs, src/Agendamento.Api/Infrastructure/Identity/InMemoryIdentityStore.cs, src/Agendamento.Api/Infrastructure/Identity/JwtSessionIssuer.cs, src/Agendamento.Api/Infrastructure/Identity/SystemClock.cs, src/Agendamento.Api/Features/Identity/SessionEndpoints.cs, tests/Agendamento.IntegrationTests/Identity/SessionEndpointTests.cs
- Notas: Implementar `POST /api/v1/auth/sessions`; não incluir CPF no token.
