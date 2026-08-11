namespace Agendamento.Api.Application.Identity.GetUserProfile;

public record UserProfileResponse(
    Guid Id,
    string Name,
    string Email,
    string Role,
    Guid TenantId,
    string[] Permissions,
    string Status
);
