namespace Agendamento.Application.Identity.CreateSession;

/// <summary>
/// Port for looking up credentials and the active tenant membership selected by the system.
/// The HTTP request must never select a tenant.
/// </summary>
public interface IIdentityAuthenticationStore
{
    Task<AuthenticationIdentity?> FindByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default);
}

public sealed record AuthenticationIdentity(
    Guid UserId,
    string PasswordHash,
    bool IsEmailVerified,
    bool IsActive,
    ActiveTenantMembership? ActiveMembership);

public sealed record ActiveTenantMembership(Guid TenantId, string Role);
