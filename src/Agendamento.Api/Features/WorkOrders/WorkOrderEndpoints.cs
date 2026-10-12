using Agendamento.Api.Infrastructure.Authorization;
using Agendamento.Api.Features.Common;
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
            try
            {
                var id = await handler.HandleAsync(command, cancellationToken);
                return Results.Created($"/api/v1/work-orders/{id}", new CreatedIdResponse(id));
            }
            catch (InvalidOperationException ex) when (ex.Message == "Quotation not found.")
            {
                return Results.Problem(statusCode: 400, title: "Orçamento não encontrado.");
            }
            catch (InvalidOperationException ex) when (ex.Message == "Quotation must be approved before converting to a work order.")
            {
                return Results.Problem(statusCode: 400, title: "O orçamento deve ser aprovado antes da conversão.");
            }
        })
        .WithName("ConvertQuotationToWorkOrder")
        .Produces<CreatedIdResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .RequirePermission("sales.write");

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
        .WithName("GetWorkOrderById")
        .Produces<WorkOrderResponse>()
        .Produces(StatusCodes.Status404NotFound)
        .RequirePermission("sales.read");

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
            
            try
            {
                await handler.HandleAsync(command, cancellationToken);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex) when (ex.Message == "Work Order not found.")
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException ex) when (ex.Message == "Cannot update status of a completed or cancelled work order.")
            {
                return Results.Problem(statusCode: 400, title: "Não é possível alterar uma ordem concluída ou cancelada.");
            }
        })
        .WithName("UpdateWorkOrderStatus")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .RequirePermission("sales.write");
    }
}

public class UpdateWorkOrderStatusRequest
{
    public WorkOrderStatus Status { get; set; }
    public string? Notes { get; set; }
}
