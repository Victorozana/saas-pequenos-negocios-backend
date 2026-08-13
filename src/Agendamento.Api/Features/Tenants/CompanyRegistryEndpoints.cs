using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Agendamento.Api.Application.Tenants.LookupCompany;
using Agendamento.Api.Features.Common;
using System;
using System.Threading;

namespace Agendamento.Api.Features.Tenants;

public static class CompanyRegistryEndpoints
{
    public static void MapCompanyRegistryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/company-registry")
            .WithTags("Company Registry")
            .RequireRateLimiting("CompanyRegistryLimit");

        group.MapGet("/{cnpj}", async (
            string cnpj,
            [FromServices] LookupCompanyHandler handler,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var query = new LookupCompanyQuery(cnpj);
                var result = await handler.HandleAsync(query, cancellationToken);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ProblemDetailsExtensions.Create("CNPJ inválido.", ex.Message));
            }
            catch (CompanyRegistryNotFoundException ex)
            {
                return Results.NotFound(ProblemDetailsExtensions.Create(ex.Message, ex.Message));
            }
            catch (CompanyRegistryInactiveException ex)
            {
                return Results.UnprocessableEntity(ProblemDetailsExtensions.Create(ex.Message, ex.Message));
            }
            catch (CompanyRegistryUnavailableException ex)
            {
                return Results.Json(
                    ProblemDetailsExtensions.Create(ex.Message, ex.Message),
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }).WithName("LookupCompanyRegistry");
    }
}
