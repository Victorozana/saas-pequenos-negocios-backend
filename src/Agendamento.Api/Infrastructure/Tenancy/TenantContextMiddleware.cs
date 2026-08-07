using Agendamento.Api.Application.Tenancy;

namespace Agendamento.Api.Infrastructure.Tenancy;

public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        // AC-029: we ensure that any tenant_id from query/header is ignored.
        // The HttpTenantContext strictly reads from claims.
        // This middleware can optionally enforce early failures if needed.
        
        // Just proceed, the HttpTenantContext securely wraps the claims.
        await _next(context);
    }
}
