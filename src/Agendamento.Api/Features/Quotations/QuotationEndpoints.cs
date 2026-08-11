using Agendamento.Api.Application.Quotations.CreateQuotation;
using Agendamento.Api.Application.Quotations.ExportPdf;
using Agendamento.Api.Application.Quotations.GetQuotations;
using Agendamento.Api.Application.Quotations.UpdateQuotationStatus;
using Agendamento.Api.Application.WorkOrders.ConvertQuotationToWorkOrder;
using Microsoft.AspNetCore.Mvc;

namespace Agendamento.Api.Features.Quotations;

public static class QuotationEndpoints
{
    public static void MapQuotationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/quotations")
            .WithTags("Quotations")
            .RequireAuthorization();

        group.MapPost("/", async (
            [FromBody] CreateQuotationCommand command,
            [FromServices] CreateQuotationHandler handler,
            CancellationToken ct) =>
        {
            var id = await handler.HandleAsync(command, ct);
            return Results.Created($"/api/v1/quotations/{id}", new { id });
        })
        .WithName("CreateQuotation")
        .Produces(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/", async (
            [AsParameters] GetQuotationsQuery query,
            [FromServices] GetQuotationsHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(query, ct);
            return Results.Ok(result);
        })
        .WithName("GetQuotations")
        .Produces<PagedResult<QuotationListDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] GetQuotationByIdHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(id, ct);
            return result != null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetQuotationById")
        .Produces<QuotationDetailDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPatch("/{id:guid}/status", async (
            Guid id,
            [FromBody] UpdateQuotationStatusCommand command,
            [FromServices] UpdateQuotationStatusHandler handler,
            CancellationToken ct) =>
        {
            var success = await handler.HandleAsync(id, command, ct);
            return success ? Results.NoContent() : Results.NotFound();
        })
        .WithName("UpdateQuotationStatus")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/{id:guid}/convert", async (
            Guid id,
            [FromServices] ConvertQuotationToWorkOrderHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var workOrderId = await handler.HandleAsync(new ConvertQuotationToWorkOrderCommand { QuotationId = id }, ct);
                return Results.Ok(new { WorkOrderId = workOrderId });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("ConvertQuotationToWorkOrderInQuotation")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:guid}/pdf", async (
            Guid id,
            [FromServices] GetQuotationByIdHandler getHandler,
            [FromServices] IQuotationPdfGenerator pdfGenerator,
            CancellationToken ct) =>
        {
            var quotation = await getHandler.HandleAsync(id, ct);
            
            if (quotation == null)
                return Results.NotFound();

            var pdfBytes = await pdfGenerator.GeneratePdfAsync(quotation, ct);

            return Results.File(pdfBytes, "application/pdf", $"Orcamento-{quotation.Code}.pdf");
        })
        .WithName("ExportQuotationPdf")
        .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
