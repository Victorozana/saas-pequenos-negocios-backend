using Agendamento.Api.Infrastructure.Authorization;
using Agendamento.Api.Application.Services.CreateService;
using Agendamento.Api.Application.Services.GetServices;
using Agendamento.Api.Application.Services.UpdateService;
using Microsoft.AspNetCore.Mvc;

namespace Agendamento.Api.Features.Services;

public static class ServiceEndpoints
{
    public static void MapServiceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/services")
            .WithTags("Services")
            .RequireAuthorization();

        group.MapPost("/", async (
            [FromBody] CreateServiceCommand command,
            [FromServices] CreateServiceHandler handler,
            CancellationToken ct) =>
        {
            var id = await handler.HandleAsync(command, ct);
            return Results.Created($"/api/v1/services/{id}", new { id });
        })
        .WithName("CreateService")
        .RequirePermission("services.write")
        .Produces(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/", async (
            [AsParameters] GetServicesQuery query,
            [FromServices] GetServicesHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(query, ct);
            return Results.Ok(result);
        })
        .WithName("GetServices")
        .RequirePermission("services.read")
        .Produces<PagedResult<ServiceItemDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] GetServiceByIdHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(id, ct);
            return result != null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetServiceById")
        .RequirePermission("services.read")
        .Produces<ServiceItemDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateServiceCommand command,
            [FromServices] UpdateServiceHandler handler,
            CancellationToken ct) =>
        {
            var success = await handler.HandleAsync(id, command, ct);
            return success ? Results.NoContent() : Results.NotFound();
        })
        .WithName("UpdateService")
        .RequirePermission("services.write")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
