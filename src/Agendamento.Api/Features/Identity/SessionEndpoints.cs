using Agendamento.Application.Identity.CreateSession;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Agendamento.Api.Features.Identity;

public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/auth/sessions", CreateAsync)
                 .WithName("CreateSession")
                 .WithTags("Identity");
        return endpoints;
    }

    private static async Task<Results<Ok<SessionResponse>, ProblemHttpResult>> CreateAsync(
        CreateSessionRequest request,
        CreateSessionHandler handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Dados de acesso inválidos.");
        }

        var outcome = await handler.HandleAsync(new CreateSessionCommand(request.Email, request.Password), cancellationToken);
        if (outcome.Error == "email_verification_required")
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Confirme seu e-mail antes de entrar.");
        }

        if (outcome.Error is not null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Não foi possível iniciar a sessão.");
        }

        return TypedResults.Ok(new SessionResponse(outcome.AccessToken!, outcome.ExpiresAt!.Value));
    }
}

public sealed record CreateSessionRequest(string Email, string Password);

public sealed record SessionResponse(string AccessToken, DateTimeOffset ExpiresAt);
