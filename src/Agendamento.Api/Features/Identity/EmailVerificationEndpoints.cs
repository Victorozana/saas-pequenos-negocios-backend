using Agendamento.Application.Identity.VerifyEmail;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Agendamento.Api.Features.Identity;

public static class EmailVerificationEndpoints
{
    public static IEndpointRouteBuilder MapEmailVerificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/auth/email-verifications", ConfirmAsync);
        return endpoints;
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ConfirmAsync(
        VerifyEmailRequest request,
        VerifyEmailHandler handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Não foi possível concluir a verificação.");
        }

        var result = await handler.HandleAsync(new VerifyEmailCommand(request.Token), cancellationToken);
        return result == VerifyEmailResult.Verified
            ? TypedResults.NoContent()
            : TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Não foi possível concluir a verificação.");
    }
}
