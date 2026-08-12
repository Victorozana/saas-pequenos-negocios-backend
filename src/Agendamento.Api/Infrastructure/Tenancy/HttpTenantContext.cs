using Agendamento.Api.Application.Tenancy;

namespace Agendamento.Api.Infrastructure.Tenancy;

public class HttpTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid TenantId => GetTenantIdFromClaims() ?? Guid.Empty;

    public bool HasTenant => GetTenantIdFromClaims() != null;

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
