using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.Api.Infrastructure.Authorization;
using Agendamento.Api.Features.Common;
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


        group.MapPost("/from-quotation/{quotationId:guid}", (
            Guid quotationId, [FromHeader(Name = "Idempotency-Key")] Guid key,
            ScheduleWorkOrderRequest request, ClaimsPrincipal principal, WorkOrderSchedule schedule, CancellationToken ct) =>
            schedule.SaveAsync(principal, quotationId, key, request, true, false, ct))
            .WithName("ConvertQuotationToWorkOrder")
            .RequirePermission("sales.write")
            .Produces<CreatedIdResponse>(201).ProducesProblem(400).ProducesProblem(409).Produces(404)
            .AddOpenApiOperationTransformer((operation, _, _) =>
            {
                if (operation.Responses?["201"] is Microsoft.OpenApi.OpenApiResponse response)
                    response.Headers = new Dictionary<string, Microsoft.OpenApi.IOpenApiHeader>
                    {
                        ["Location"] = new Microsoft.OpenApi.OpenApiHeader
                        {
                            Description = "Caminho relativo da OS.",
                            Schema = new Microsoft.OpenApi.OpenApiSchema { Type = Microsoft.OpenApi.JsonSchemaType.String }
                        }
                    };
                return Task.CompletedTask;
            });

        group.MapGet("/participants", async (ClaimsPrincipal principal, WorkOrderSchedule schedule, CancellationToken ct) =>
        {
            if ((await schedule.ActorAsync(principal, ct))?.CanManage != true) return Results.Forbid();
            return Results.Ok(await schedule.Members().Where(m => m.IsActive).OrderBy(m => m.Name).ToListAsync(ct));
        }).WithName("GetWorkOrderParticipants").Produces<List<ScheduleParticipant>>().Produces(403);

        group.MapPut("/{id:guid}/schedule", (Guid id, ScheduleWorkOrderRequest request,
            ClaimsPrincipal principal, WorkOrderSchedule schedule, CancellationToken ct) =>
            schedule.SaveAsync(principal, id, null, request, false, false, ct))
            .WithName("UpdateWorkOrderSchedule").RequirePermission("sales.write")
            .Produces(204).Produces(404).ProducesProblem(400).ProducesProblem(409);

        group.MapGet("/", async (Guid? quotationId, Guid? participantId, DateTimeOffset? start, DateTimeOffset? end,
            WorkOrderStatus? status, int? page, int? pageSize, ClaimsPrincipal principal, WorkOrderSchedule schedule,
            Agendamento.Api.Infrastructure.Persistence.AgendamentoDbContext db, CancellationToken ct) =>
        {
            var actor = await schedule.ActorAsync(principal, ct);
            if (actor == null || !actor.Membership.HasPermission("appointments.read")) return Results.Forbid();
            if (!actor.IsAdministrator && participantId.HasValue && participantId != actor.Id) return Results.NotFound();
            var orders = schedule.VisibleOrders(actor);
            if (quotationId.HasValue) orders = orders.Where(w => w.QuotationId == quotationId);
            if (participantId.HasValue) orders = orders.Where(w => schedule.ParticipantOrders(participantId.Value).Contains(w.Id));
            if (status.HasValue) orders = orders.Where(w => w.Status == status);
            if (start.HasValue || end.HasValue)
            {
                var from = start?.UtcDateTime;
                var to = end?.UtcDateTime;
                orders = orders.Where(w => db.Appointments.Any(a => a.WorkOrderId == w.Id
                    && (!from.HasValue || a.EndTime > from) && (!to.HasValue || a.StartTime < to)));
            }
            var currentPage = Math.Clamp(page ?? 1, 1, 100000);
            var size = Math.Clamp(pageSize ?? 25, 1, 100);
            var total = await orders.CountAsync(ct);
            var items = new List<ScheduledWorkOrder>();
            foreach (var order in await orders.OrderByDescending(w => w.CreatedAt).ThenBy(w => w.Id)
                .Skip((currentPage - 1) * size).Take(size).ToListAsync(ct))
                items.Add(await schedule.DescribeAsync(order, ct));
            return Results.Ok(new WorkOrderPage(items, currentPage, size, total));
        }).WithName("ListWorkOrders").Produces<WorkOrderPage>().Produces(403).Produces(404);

        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal principal, WorkOrderSchedule schedule,
            GetWorkOrderByIdHandler handler, CancellationToken ct) =>
        {
            var actor = await schedule.ActorAsync(principal, ct);
            if (actor == null || !actor.Membership.HasPermission("appointments.read") && !actor.Membership.HasPermission("sales.read"))
                return Results.Forbid();
            var order = await schedule.VisibleOrders(actor).SingleOrDefaultAsync(w => w.Id == id, ct);
            if (order == null) return Results.NotFound();
            var scheduled = await schedule.DescribeAsync(order, ct);
            var result = actor.Membership.HasPermission("sales.read")
                ? await handler.HandleAsync(new GetWorkOrderByIdQuery { Id = id }, ct)
                : WorkOrderResponse.FromEntity(order);
            if (result == null) return Results.NotFound();
            if (!actor.Membership.HasPermission("sales.read")) result.Items.Clear();
            result.Schedule = scheduled;
            return Results.Ok(result);
        }).WithName("GetWorkOrderById").Produces<WorkOrderResponse>().Produces(403).Produces(404);

        group.MapPatch("/{id:guid}/status", async (
            Guid id,
            [FromBody] UpdateWorkOrderStatusRequest request,
            [FromServices] UpdateWorkOrderStatusHandler handler,
            ClaimsPrincipal principal, WorkOrderSchedule schedule, AgendamentoDbContext db, CancellationToken cancellationToken) =>
        {
            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
            await schedule.LockAsync(cancellationToken);
            var actor = await schedule.ActorAsync(principal, cancellationToken);
            if (actor == null || !actor.Membership.HasPermission("sales.write")) return Results.Forbid();
            var order = await schedule.VisibleOrders(actor).SingleOrDefaultAsync(w => w.Id == id, cancellationToken);
            if (order == null) return Results.NotFound();
            if (!Enum.IsDefined(request.Status)) return Results.Problem(statusCode: 400, title: "Situação inválida.");
            if (request.Status is not (WorkOrderStatus.Scheduled or WorkOrderStatus.Cancelled) &&
                (await schedule.DescribeAsync(order, cancellationToken)).NeedsRegularization)
                return Results.Problem(statusCode: 409, title: "Defina equipe e horário antes de avançar a ordem.");
            var command = new UpdateWorkOrderStatusCommand
            {
                Id = id,
                Status = request.Status,
                Notes = request.Notes
            };
            
            try
            {
                await handler.HandleAsync(command, cancellationToken);
                if (transaction != null) await transaction.CommitAsync(cancellationToken);
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
        .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(409)
        .RequirePermission("sales.write");
    }
}

public class UpdateWorkOrderStatusRequest
{
    public WorkOrderStatus Status { get; set; }
    public string? Notes { get; set; }
}
