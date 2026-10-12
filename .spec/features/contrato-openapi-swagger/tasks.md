# Tasks: Contrato OpenAPI e Swagger

> feature: contrato-openapi-swagger

## T-022 — Registrar OpenAPI e Swagger UI [concluida]

- Refs: US-015, US-016, AC-036, AC-040
- Arquivos: src/Agendamento.Api/Agendamento.Api.csproj, src/Agendamento.Api/Program.cs, src/Agendamento.Api/OpenApi/OpenApiExtensions.cs, tests/Agendamento.IntegrationTests/OpenApi/SwaggerUiTests.cs
- Notas: UI apenas em Development/Test; documento em `/openapi/v1.json`.

## T-023 — Documentar endpoints, segurança e erros [pendente]
- Refs: US-015, AC-036, AC-037, AC-038
- Arquivos: src/Agendamento.Api/Program.cs, src/Agendamento.Api/Features/Identity/CurrentUserEndpoints.cs, src/Agendamento.Api/Features/Identity/EmailVerificationEndpoints.cs, src/Agendamento.Api/Features/Identity/SessionEndpoints.cs, src/Agendamento.Api/Features/Identity/VerifyEmailRequest.cs, src/Agendamento.Api/Features/Tenants/CompanyRegistryEndpoints.cs, src/Agendamento.Api/Features/Tenants/CurrentTenantEndpoints.cs, src/Agendamento.Api/Features/Tenants/RegisterTenantRequest.cs, src/Agendamento.Api/Features/Tenants/RegisterTenantResponse.cs, src/Agendamento.Api/Features/Tenants/TenantRegistrationEndpoints.cs, src/Agendamento.Api/OpenApi/ProblemDetailsOperationTransformer.cs, src/Agendamento.Api/OpenApi/BearerSecuritySchemeTransformer.cs, tests/Agendamento.ArchitectureTests/OpenApi/EndpointMetadataTests.cs, src/Agendamento.Api/Features/Common/ContractResponses.cs, src/Agendamento.Api/Features/Customers/CustomerEndpoints.cs, src/Agendamento.Api/Application/Customers/GetCustomers/GetCustomersHandler.cs, src/Agendamento.Api/Application/Customers/GetCustomers/GetCustomerByIdHandler.cs, src/Agendamento.Api/Application/Customers/UpdateCustomer/UpdateCustomerCommand.cs, src/Agendamento.Api/Application/Customers/UpdateCustomer/UpdateCustomerHandler.cs, src/Agendamento.Api/Features/Appointments/AppointmentEndpoints.cs, src/Agendamento.Api/Features/Services/ServiceEndpoints.cs, src/Agendamento.Api/Features/Quotations/QuotationEndpoints.cs, src/Agendamento.Api/Features/Notifications/NotificationEndpoints.cs, src/Agendamento.Api/Features/Subscriptions/SubscriptionEndpoints.cs
- Notas: Minimização e schemas JSON ausentes corrigidos nesta revisão; Bearer documentado por operação respeitando AllowAnonymous. Manter pendente: os erros de notificações/conversão usam objetos message/error apesar de outros endpoints documentarem ProblemDetails; revisar também respostas sem corpo e os exports binários. PASS granular de AC-037 não certifica todas essas divergências como corrigidas.

## T-024 — Gerar contrato canônico versionado [concluida]

- Refs: US-016, AC-039
- Arquivos: docs/openapi/v1.json, tools/export-openapi.ps1, tests/Agendamento.IntegrationTests/OpenApi/OpenApiSnapshotTests.cs
- Notas: Comparação semântica/canônica, sem depender de ordenação ou timestamps.

## T-025 — Validar completude e minimização dos schemas [concluida]
- Refs: US-015, US-016, AC-036, AC-037, AC-038, AC-039
- Arquivos: tests/Agendamento.ArchitectureTests/OpenApi/OpenApiCompletenessTests.cs, tests/Agendamento.ArchitectureTests/OpenApi/OpenApiSensitiveDataTests.cs, tests/Agendamento.ArchitectureTests/OpenApi/OpenApiDataMinimization.cs, tests/Agendamento.IntegrationTests/Customers/CustomerIsolationTests.cs, tests/Agendamento.IntegrationTests/Tenants/TenantOnboardingFlowTests.cs
- Notas: Percorrer usos em respostas e headers, resolver referências locais com proteção contra ciclos, arrays, allOf/oneOf/anyOf e additionalProperties. Inspecionar exemplos de entrada/saída, incluindo referências e JSON serializado. Entradas legítimas não são saídas; accessToken somente no sucesso do login. Casos negativos validam o detector.
