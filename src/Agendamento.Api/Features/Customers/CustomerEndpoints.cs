using Agendamento.Api.Application.Customers.CreateCustomer;
using Agendamento.Api.Application.Customers.GetCustomers;
using Agendamento.Api.Application.Customers.UpdateCustomer;
using Microsoft.AspNetCore.Mvc;

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
            var id = await handler.HandleAsync(command, cancellationToken);
            return Results.Created($"/api/v1/customers/{id}", new { Id = id });
        })
        .WithName("CreateCustomer")
        .WithSummary("Creates a new customer for the current tenant");

        group.MapPut("{id:guid}", async (
            [FromRoute] Guid id,
            [FromBody] UpdateCustomerCommand command,
            [FromServices] UpdateCustomerHandler handler,
            CancellationToken cancellationToken) =>
        {
            if (id != command.CustomerId)
                return Results.BadRequest("ID mismatch");

            var success = await handler.HandleAsync(command, cancellationToken);
            if (!success)
                return Results.NotFound();

            return Results.NoContent();
        })
        .WithName("UpdateCustomer")
        .WithSummary("Updates an existing customer");

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
        .WithSummary("Lists all active customers for the current tenant");

        group.MapGet("{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] GetCustomerByIdHandler handler,
            CancellationToken cancellationToken) =>
        {
            var customer = await handler.HandleAsync(id, cancellationToken);
            return customer != null ? Results.Ok(customer) : Results.NotFound();
        })
        .WithName("GetCustomerById")
        .WithSummary("Gets a specific customer by ID");
    }
}
