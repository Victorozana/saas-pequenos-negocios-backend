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

        group.MapGet("/me", () =>
        {
            return Results.Ok(new { message = "User Profile" });
        });
    }
}
