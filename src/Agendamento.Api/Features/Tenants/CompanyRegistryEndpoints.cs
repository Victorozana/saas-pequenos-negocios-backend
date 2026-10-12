using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Agendamento.Api.Application.Tenants.LookupCompany;
using Agendamento.Api.Application.Tenants.CompanyRegistry;
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
            catch (ArgumentException)
            {
                return Results.Problem(statusCode: 400, title: "CNPJ inválido.");
            }
            catch (CompanyRegistryNotFoundException)
            {
                return Results.Problem(statusCode: 404, title: "Empresa não encontrada.");
            }
            catch (CompanyRegistryInactiveException)
            {
                return Results.Problem(statusCode: 422, title: "Empresa inativa.");
            }
            catch (CompanyRegistryUnavailableException)
            {
                return Results.Problem(statusCode: 503, title: "Consulta cadastral indisponível. Tente novamente mais tarde.");
            }
        })
        .WithName("LookupCompanyRegistry")
        .Produces<CompanyRegistryResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status429TooManyRequests)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }
}
