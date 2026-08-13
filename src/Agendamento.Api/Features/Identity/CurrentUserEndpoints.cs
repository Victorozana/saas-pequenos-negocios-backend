using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Agendamento.Api.Features.Identity;

public static class CurrentUserEndpoints
{
    public static void MapCurrentUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users")
            .WithTags("Identity")
            .RequireAuthorization();

        group.MapGet("/me", async (
            Agendamento.Api.Infrastructure.Persistence.AgendamentoDbContext db,
            System.Security.Claims.ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var userIdStr = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId)) return Results.Unauthorized();

            var userEntity = await db.Users.FindAsync(new object[] { userId }, cancellationToken);
            if (userEntity is null) return Results.NotFound();

            var membership = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                db.TenantMemberships, m => m.UserId == userId, cancellationToken);
            
            if (membership is null) return Results.Forbid();

            var response = new Agendamento.Api.Application.Identity.GetUserProfile.UserProfileResponse(
                userEntity.Id,
                userEntity.Name,
                userEntity.Email,
                membership.Role,
                membership.TenantId,
                membership.Permissions.ToArray(),
                userEntity.Status.ToString()
            );

            return Results.Ok(response);
        }).WithName("GetCurrentUser")
        .Produces<Agendamento.Api.Application.Identity.GetUserProfile.UserProfileResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
