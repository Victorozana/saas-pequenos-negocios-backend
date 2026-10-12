using Agendamento.Api.Application.Customers.CreateCustomer;
using Agendamento.Api.Application.Customers.GetCustomers;
using Agendamento.Api.Application.Customers.UpdateCustomer;
using Agendamento.Api.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;
using Agendamento.Api.Features.Common;

namespace Agendamento.Api.Features.Customers;

public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("api/v1/customers")
            .RequireAuthorization()
            .WithTags("Customers");

        group.MapPost("", async (
            [FromBody] CreateCustomerCommand command,
            [FromServices] CreateCustomerHandler handler,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var id = await handler.HandleAsync(command, cancellationToken);
                return Results.Created($"/api/v1/customers/{id}", new CreatedIdResponse(id));
            }
            catch (ArgumentException)
            {
                return Results.Problem(statusCode: 400, title: "Dados do cliente inválidos.");
            }
        })
        .WithName("CreateCustomer")
        .Produces<CreatedIdResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Creates a new customer for the current tenant")
        .RequirePermission("customers.write");

        group.MapPut("{id:guid}", async (
            [FromRoute] Guid id,
            [FromBody] UpdateCustomerCommand command,
            [FromServices] UpdateCustomerHandler handler,
            CancellationToken cancellationToken) =>
        {
            if (id != command.CustomerId)
                return Results.Problem(statusCode: 400, title: "O identificador do cliente difere da rota.");

            try
            {
                var success = await handler.HandleAsync(command, cancellationToken);
                if (!success)
                    return Results.NotFound();

                return Results.NoContent();
            }
            catch (ArgumentException)
            {
                return Results.Problem(statusCode: 400, title: "Dados do cliente inválidos.");
            }
        })
        .WithName("UpdateCustomer")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Updates an existing customer")
        .RequirePermission("customers.write");

        group.MapGet("", async (
            [FromQuery] string? search,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromServices] GetCustomersHandler handler,
            CancellationToken cancellationToken) =>
        {
            var results = await handler.HandleAsync(search, page ?? 1, pageSize ?? 20, cancellationToken);
            return Results.Ok(results);
        })
        .WithName("GetCustomers")
        .Produces<PaginatedResult<CustomerResponse>>()
        .WithSummary("Lists all active customers for the current tenant")
        .RequirePermission("customers.read");

        group.MapGet("{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] GetCustomerByIdHandler handler,
            CancellationToken cancellationToken) =>
        {
            var customer = await handler.HandleAsync(id, cancellationToken);
            return customer != null ? Results.Ok(customer) : Results.NotFound();
        })
        .WithName("GetCustomerById")
        .Produces<CustomerResponse>()
        .Produces(StatusCodes.Status404NotFound)
        .WithSummary("Gets a specific customer by ID")
        .RequirePermission("customers.read");
    }
}
