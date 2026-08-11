using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Agendamento.Api.Infrastructure.Authorization;

public static class PermissionAuthorizationExtensions
{
    public static RouteHandlerBuilder RequirePermission(this RouteHandlerBuilder builder, string permission)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var httpContext = context.HttpContext;
            var user = httpContext.User;

            if (user?.Identity?.IsAuthenticated != true)
            {
                return Results.Unauthorized();
            }

            var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Results.Unauthorized();
            }

            var tenantContext = httpContext.RequestServices.GetRequiredService<ITenantContext>();
            if (!tenantContext.HasTenant)
            {
                return Results.Problem(statusCode: 403, title: "Forbidden", detail: "Contexto de tenant não encontrado.");
            }

            var dbContext = httpContext.RequestServices.GetRequiredService<AgendamentoDbContext>();
            var membership = await dbContext.TenantMemberships
                .FirstOrDefaultAsync(m => m.TenantId == tenantContext.TenantId && m.UserId == userId);

            if (membership == null || !membership.HasPermission(permission))
            {
                return Results.Problem(
                    statusCode: 403,
                    title: "Forbidden",
                    detail: $"Acesso negado. Requer a permissão: {permission}"
                );
            }

            return await next(context);
        });
    }
}
