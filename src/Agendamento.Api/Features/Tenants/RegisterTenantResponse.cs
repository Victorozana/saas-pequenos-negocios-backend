namespace Agendamento.Api.Features.Tenants;

public record RegisterTenantResponse(
    string Message,
    string? VerificationToken = null
);
