using Agendamento.Api.Features.Common;
using Agendamento.Api.Infrastructure.Authorization;
using Agendamento.Api.Application.Quotations.CreateQuotation;
using Agendamento.Api.Application.Quotations.ExportPdf;
using Agendamento.Api.Application.Quotations.GetQuotations;
using Agendamento.Api.Application.Quotations.UpdateQuotationStatus;
using Agendamento.Api.Application.Quotations.UpdateQuotation;
using Agendamento.Api.Application.WorkOrders.ConvertQuotationToWorkOrder;
using Microsoft.AspNetCore.Mvc;
using Agendamento.Api.OpenApi;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
            try
            {
                var id = await handler.HandleAsync(command, ct);
                return Results.Created($"/api/v1/quotations/{id}", new CreatedIdResponse(id));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Results.Problem(statusCode: 400, title: "Não foi possível criar o orçamento. Verifique os dados informados.");
            }
            catch (DbUpdateException ex) when (IsDuplicateCode(ex))
            {
                return Results.Problem(statusCode: 400, title: "Não foi possível criar o orçamento. Verifique os dados informados.");
            }
        })
        .WithName("CreateQuotation")
        .WithDescription("Cria orçamento Pending no tenant autenticado. Código único por empresa (até 50 caracteres); notas e termos até 2000, notas do sinal até 1000. Datas devem usar UTC (sufixo Z). Entradas inválidas ou código repetido retornam 400 sem persistência parcial.")
        .RequirePermission("sales.write")
        .Produces<CreatedIdResponse>(StatusCodes.Status201Created)
        .AddOpenApiOperationTransformer((operation, context, ct) =>
        {
            if (operation.Responses?["201"] is Microsoft.OpenApi.OpenApiResponse response)
            {
                response.Headers ??= new Dictionary<string, Microsoft.OpenApi.IOpenApiHeader>();
                response.Headers["Location"] = new Microsoft.OpenApi.OpenApiHeader
                {
                    Description = "Caminho relativo do orçamento criado, acessível com sales.read.",
                    Schema = new Microsoft.OpenApi.OpenApiSchema { Type = Microsoft.OpenApi.JsonSchemaType.String }
                };
            }
            return Task.CompletedTask;
        })
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/", async (
            [AsParameters] GetQuotationsQuery query,
            [FromServices] GetQuotationsHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleAsync(query, ct);
                return Results.Ok(result);
            }
            catch (ArgumentException)
            {
                return Results.Problem(statusCode: 400, title: "Paginação ou filtro de orçamento inválido.");
            }
        })
        .WithName("GetQuotations")
        .WithDescription("Lista apenas orçamentos do tenant autenticado. Busca por código ou nome do cliente, sem distinguir maiúsculas/minúsculas; status deve ser um valor definido. Página >= 1, tamanho de 1 a 100 (padrões 1/20); deslocamento máximo int32. Total considera os filtros. Ordenação por IssueDate decrescente e Id crescente para desempate.")
        .RequirePermission("sales.read")
        .Produces<PagedResult<QuotationListDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] GetQuotationByIdHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(id, ct);
            return result != null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetQuotationById")
        .RequirePermission("sales.read")
        .Produces<QuotationDetailDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateQuotationCommand command,
            [FromServices] UpdateQuotationHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var success = await handler.HandleAsync(id, command, ct);
                return success ? Results.NoContent() : Results.NotFound();
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Não foi possível editar o orçamento. Verifique os dados e o estado atual.");
            }
            catch (DbUpdateException ex) when (IsDuplicateCode(ex))
            {
                return Results.Problem(statusCode: 400, title: "Não foi possível editar o orçamento. Verifique os dados e o estado atual.");
            }
        })
        .WithName("UpdateQuotation")
        .WithDescription("Substitui conteúdo somente em Pending. Código único por empresa (até 50 caracteres); notas e termos até 2000, notas do sinal até 1000. Datas devem usar UTC (sufixo Z). Validação antecede alterações; estados finais e edição externa são bloqueados.")
        .RequirePermission("sales.write")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPatch("/{id:guid}/status", async (
            Guid id,
            [FromBody] UpdateQuotationStatusCommand command,
            [FromServices] UpdateQuotationStatusHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var success = await handler.HandleAsync(id, command, ct);
                return success ? Results.NoContent() : Results.NotFound();
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Results.Problem(statusCode: 400, title: "Não foi possível alterar o estado do orçamento.");
            }
        })
        .WithName("UpdateQuotationStatus")
        .RequirePermission("sales.write")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/{id:guid}/convert", async (
            Guid id,
            [FromServices] ConvertQuotationToWorkOrderHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var workOrderId = await handler.HandleAsync(new ConvertQuotationToWorkOrderCommand { QuotationId = id }, ct);
                return Results.Ok(new WorkOrderConversionResponse(workOrderId));
            }
            catch (InvalidOperationException)
            {
                return Results.Problem(statusCode: 400, title: "Não foi possível converter o orçamento em ordem de serviço.");
            }
        })
        .WithName("ConvertQuotationToWorkOrderInQuotation")
        .RequirePermission("sales.write")
        .Produces<WorkOrderConversionResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

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
        .RequirePermission("sales.read")
        .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
        .DocumentDownload("application/pdf", true)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized);
    }

    private static bool IsDuplicateCode(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_Quotations_tenant_id_Code"
        };
}

public record WorkOrderConversionResponse(Guid WorkOrderId);
