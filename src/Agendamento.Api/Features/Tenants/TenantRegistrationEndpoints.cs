using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Agendamento.Api.Application.Tenants.RegisterTenant;
using Agendamento.Api.Features.Common;
using System;
using System.Threading;

namespace Agendamento.Api.Features.Tenants;

public static class TenantRegistrationEndpoints
{
    public static void MapTenantRegistrationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tenants")
            .WithTags("Tenants");

        group.MapPost("/", async (
            [FromBody] RegisterTenantRequest request,
            [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
            [FromServices] RegisterTenantHandler handler,
            [FromServices] Microsoft.AspNetCore.Hosting.IWebHostEnvironment environment,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
            {
                return Results.BadRequest(ProblemDetailsExtensions.Create("Header Missing", "O header Idempotency-Key é obrigatório."));
            }

            var command = new RegisterTenantCommand(
                request.Cnpj, request.CompanyName, request.TradeName, request.LegalNature, request.PrimaryCnae, request.CategoryId,
                request.Email, request.Phone, request.AddressStreet, request.AddressNumber, request.AddressComplement,
                request.AddressNeighborhood, request.AddressCity, request.AddressState, request.AddressZipCode,
                request.StateRegistration, request.MunicipalRegistration, request.IsTaxExempt, request.TaxRegime, request.FiscalEmail,
                request.AdminName, request.AdminCpf, request.AdminEmail, request.AdminPassword, request.IsLegalRepresentative, idempotencyKey
            );

            try
            {
                var rawToken = await handler.HandleAsync(command, cancellationToken);
                
                string? verificationToken = null;
                if (environment.IsDevelopment())
                {
                    verificationToken = rawToken;
                    httpContext.Response.Headers["X-Development-Verification-Token"] = rawToken;
                }

                return Results.Created($"/api/v1/tenants/me", new RegisterTenantResponse("Cadastro realizado com sucesso.", verificationToken));
            }
            catch (Exception ex)
            {
                return Results.UnprocessableEntity(ProblemDetailsExtensions.Create("Erro no cadastro", ex.Message));
            }
        })
        .WithName("RegisterTenant")
        .Produces<RegisterTenantResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }
}
