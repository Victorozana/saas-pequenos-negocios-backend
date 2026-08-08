using Agendamento.Api.Application.WorkOrders.ConvertQuotationToWorkOrder;
using Agendamento.Api.Application.WorkOrders.GetWorkOrderById;
using Agendamento.Api.Application.WorkOrders.UpdateWorkOrderStatus;
using Agendamento.Api.Domain.WorkOrders;
using Microsoft.AspNetCore.Mvc;

namespace Agendamento.Api.Features.WorkOrders;

public static class WorkOrderEndpoints
{
    public static void MapWorkOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/work-orders")
            .WithTags("WorkOrders")
            .RequireAuthorization();

        group.MapPost("/from-quotation/{quotationId:guid}", async (
            Guid quotationId,
            [FromServices] ConvertQuotationToWorkOrderHandler handler,
            CancellationToken cancellationToken) =>
        {
            var command = new ConvertQuotationToWorkOrderCommand { QuotationId = quotationId };
            var id = await handler.HandleAsync(command, cancellationToken);
            return Results.Created($"/api/v1/work-orders/{id}", new { Id = id });
        })
        .WithName("ConvertQuotationToWorkOrder");

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] GetWorkOrderByIdHandler handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetWorkOrderByIdQuery { Id = id };
            var result = await handler.HandleAsync(query, cancellationToken);
            
            if (result == null)
                return Results.NotFound();
                
            return Results.Ok(result);
        })
        .WithName("GetWorkOrderById");

        group.MapPatch("/{id:guid}/status", async (
            Guid id,
            [FromBody] UpdateWorkOrderStatusRequest request,
            [FromServices] UpdateWorkOrderStatusHandler handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateWorkOrderStatusCommand
            {
                Id = id,
                Status = request.Status,
                Notes = request.Notes
            };
            
            await handler.HandleAsync(command, cancellationToken);
            return Results.NoContent();
        })
        .WithName("UpdateWorkOrderStatus");
    }
}

public class UpdateWorkOrderStatusRequest
{
    public WorkOrderStatus Status { get; set; }
    public string? Notes { get; set; }
}
