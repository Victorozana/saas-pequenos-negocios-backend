using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Agendamento.Api.Features.Tenants;

public static class CurrentTenantEndpoints
{
    public static void MapCurrentTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tenants")
            .WithTags("Tenants")
            .RequireAuthorization();

        group.MapGet("/me", ([Microsoft.AspNetCore.Mvc.FromServices] Agendamento.Api.Application.Tenancy.ITenantContext tenantContext) =>
        {
            return Results.Ok(new { message = "Tenant Profile", tenantId = tenantContext.TenantId });
        }).WithName("GetCurrentTenant");
    }
}
