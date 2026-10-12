using Agendamento.Api.Application.Tenancy;

namespace Agendamento.Api.Infrastructure.Tenancy;

public class HttpTenantContext : ITenantContext
{
    private Guid? _invitationTenantId;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid TenantId => _invitationTenantId ?? GetTenantIdFromClaims() ?? Guid.Empty;

    public bool HasTenant => _invitationTenantId != null || GetTenantIdFromClaims() != null;

    // Only the server-side invitation flow can establish this scope. No request TenantId is accepted.
    internal IDisposable UseValidatedInvitation(Agendamento.Domain.Tenants.TeamInvitation invitation)
    {
        if (invitation.Status != Agendamento.Domain.Tenants.TeamInvitationStatus.Pending || invitation.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            throw new InvalidOperationException("Convite inválido.");
        var previous = _invitationTenantId;
        _invitationTenantId = invitation.TenantId;
        return new InvitationScope(() => _invitationTenantId = previous);
    }

    private sealed class InvitationScope(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }

    private Guid? GetTenantIdFromClaims()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var claim = user.FindFirst("tenant_id");
        if (claim != null && Guid.TryParse(claim.Value, out var tenantId))
        {
            return tenantId;
        }

        return null;
    }
}
