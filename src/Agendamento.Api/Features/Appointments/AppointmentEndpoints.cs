using System.Security.Claims;
using Agendamento.Api.Features.WorkOrders;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Agendamento.Api.Features.Common;
using Agendamento.Api.Infrastructure.Authorization;
using Agendamento.Api.Application.Appointments.CreateAppointment;
using Agendamento.Api.Application.Appointments.GetAppointments;
using Agendamento.Api.Application.Appointments.UpdateAppointment;
using Microsoft.AspNetCore.Mvc;

namespace Agendamento.Api.Features.Appointments;

public static class AppointmentEndpoints
{
    public static void MapAppointmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/appointments")
            .WithTags("Appointments")
            .RequireAuthorization();

        group.MapPost("/", async (
            [FromBody] CreateAppointmentCommand command,
            [FromServices] CreateAppointmentHandler handler,
            ClaimsPrincipal principal, WorkOrderSchedule schedule, AgendamentoDbContext db,
            CancellationToken cancellationToken) =>
        {
            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
            await schedule.LockAsync(cancellationToken);
            var actor = await schedule.ActorAsync(principal, cancellationToken);
            if (actor == null || !actor.Membership.HasPermission("appointments.write")) return Results.Forbid();
            if (command.WorkOrderId.HasValue)
                return Results.Problem(statusCode: 400, title: "Agende esta atividade pela ordem de serviço.");
            if (!actor.IsAdministrator)
            {
                if (command.AssignedToUserId.HasValue && command.AssignedToUserId != actor.Id) return Results.Forbid();
                command.AssignedToUserId = actor.Id;
            }
            if (command.StartTime.Kind != DateTimeKind.Utc || command.EndTime.Kind != DateTimeKind.Utc ||
                command.EndTime <= command.StartTime || !Enum.IsDefined(command.Type) ||
                command.Address?.Length > 255 || command.Notes?.Length > 1000)
                return Results.Problem(statusCode: 400, title: "Dados do agendamento inválidos.");
            if (command.AssignedToUserId is Guid assignee)
            {
                if (!await schedule.Members().AnyAsync(m => m.Id == assignee && m.IsActive, cancellationToken))
                    return Results.Problem(statusCode: 400, title: "Selecione um funcionário ativo desta empresa.");
                if (await schedule.HasConflictAsync([assignee], command.StartTime, command.EndTime, null, cancellationToken))
                    return Results.Problem(statusCode: 409, title: "O profissional já possui agendamento neste período.");
            }
            try
            {
                var id = await handler.HandleAsync(command, cancellationToken);
                if (transaction != null) await transaction.CommitAsync(cancellationToken);
                return Results.Created($"/api/v1/appointments/{id}", new CreatedIdResponse(id));
            }
            catch (ArgumentException)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Dados do agendamento inválidos.");
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("Scheduling conflict:", StringComparison.Ordinal))
            {
                return Results.Problem(statusCode: 400, title: "O profissional já possui agendamento neste período.");
            }
        })
        .WithName("CreateAppointment")
        .RequirePermission("appointments.write")
        .Produces<CreatedIdResponse>(StatusCodes.Status201Created).ProducesProblem(409)
        .ProducesProblem(StatusCodes.Status400BadRequest);


        group.MapGet("/", (DateTimeOffset? startDate, DateTimeOffset? endDate, Guid? assignedToUserId, Guid? workOrderId,
            ClaimsPrincipal principal, WorkOrderSchedule schedule, CancellationToken ct) =>
            ListAsync(principal, schedule, startDate?.UtcDateTime, endDate?.UtcDateTime, assignedToUserId, workOrderId, ct))
            .WithName("GetAppointments").Produces<List<AgendaVisit>>().RequirePermission("appointments.read");

        group.MapGet("/today", (ClaimsPrincipal principal, WorkOrderSchedule schedule, TimeProvider clock, CancellationToken ct) =>
        {
            var (start, end) = DayRange(clock.GetUtcNow());
            return ListAsync(principal, schedule, start, end, null, null, ct);
        }).WithName("GetTodayAppointments").Produces<List<AgendaVisit>>().RequirePermission("appointments.read");

        group.MapPut("/{id:guid}", async (Guid id, UpdateAppointmentRequest request,
            ClaimsPrincipal principal, WorkOrderSchedule schedule, AgendamentoDbContext db, CancellationToken ct) =>
        {
            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
            await schedule.LockAsync(ct);
            var actor = await schedule.ActorAsync(principal, ct);
            if (actor == null || !actor.Membership.HasPermission("appointments.write")) return Results.Forbid();
            if (!await schedule.VisibleAppointments(actor).AnyAsync(a => a.Id == id, ct)) return Results.NotFound();
            var appointment = await db.Appointments.SingleAsync(a => a.Id == id, ct);
            if (appointment.WorkOrderId.HasValue)
                return Results.Problem(statusCode: 409, title: "Altere o horário pela ordem de serviço.");
            var assignee = request.AssignedToUserId ?? appointment.AssignedToUserId;
            if (!actor.IsAdministrator && assignee != actor.Id) return Results.Forbid();
            var start = request.StartTime.UtcDateTime;
            var end = request.EndTime.UtcDateTime;
            if (start == default || end <= start)
                return Results.Problem(statusCode: 400, title: "O horário final deve ser posterior ao inicial.");
            if (assignee is Guid user)
            {
                if (!await schedule.Members().AnyAsync(m => m.Id == user && m.IsActive, ct))
                    return Results.Problem(statusCode: 400, title: "Selecione um funcionário ativo desta empresa.");
                if (await schedule.HasConflictAsync([user], start, end, id, ct))
                    return Results.Problem(statusCode: 409, title: "O profissional já possui agendamento neste período.");
                appointment.AssignTo(user);
            }
            appointment.Reschedule(start, end);
            await db.SaveChangesAsync(ct);
            if (transaction != null) await transaction.CommitAsync(ct);
            return Results.NoContent();
        }).WithName("UpdateAppointment").Produces(204).Produces(404).ProducesProblem(400).ProducesProblem(409)
            .RequirePermission("appointments.write");
    }

    public static (DateTime Start, DateTime End) DayRange(DateTimeOffset now, int days = 1)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var date = TimeZoneInfo.ConvertTime(now, zone).Date;
        return (TimeZoneInfo.ConvertTimeToUtc(date, zone), TimeZoneInfo.ConvertTimeToUtc(date.AddDays(days), zone));
    }

    internal static async Task<IResult> ListAsync(ClaimsPrincipal principal, WorkOrderSchedule schedule,
        DateTime? start, DateTime? end, Guid? assignee, Guid? workOrderId, CancellationToken ct)
    {
        var actor = await schedule.ActorAsync(principal, ct);
        if (actor == null || !actor.Membership.HasPermission("appointments.read")) return Results.Forbid();
        if (!actor.IsAdministrator && assignee.HasValue && assignee != actor.Id) return Results.NotFound();

        var query = schedule.VisibleAppointments(actor);
        if (start.HasValue) query = query.Where(a => a.EndTime > start);
        if (end.HasValue) query = query.Where(a => a.StartTime < end);
        if (workOrderId.HasValue) query = query.Where(a => a.WorkOrderId == workOrderId);
        if (assignee.HasValue)
        {
            var assignedOrders = schedule.ParticipantOrders(assignee.Value);
            query = query.Where(a => a.WorkOrderId == null ? a.AssignedToUserId == assignee :
                a.WorkOrderId.HasValue && assignedOrders.Contains(a.WorkOrderId.Value));
        }
        var visits = await query.OrderBy(a => a.StartTime).ThenBy(a => a.Id).Select(a => new
        {
            a.Id, a.WorkOrderId, a.Type, a.StartTime, a.EndTime, a.Address, a.Notes, a.AssignedToUserId,
            Code = a.WorkOrder == null ? null : a.WorkOrder.Code,
            Status = a.WorkOrder == null ? null : a.WorkOrder.Status.ToString()
        }).ToListAsync(ct);
        var orders = visits.Where(v => v.WorkOrderId.HasValue).Select(v => v.WorkOrderId!.Value).Distinct().ToArray();
        var people = await schedule.AppointmentParticipants(orders, visits.Select(v => v.AssignedToUserId).OfType<Guid>().ToArray(), ct);
        return Results.Ok(visits.Select(v => new AgendaVisit(v.Id, v.WorkOrderId, v.Type.ToString(), v.StartTime,
            v.EndTime, v.Address, v.Notes, v.AssignedToUserId, v.Code, v.Status,
            people.Where(p => v.WorkOrderId.HasValue ? p.WorkOrderId == v.WorkOrderId :
                p.WorkOrderId == null && p.Person.Id == v.AssignedToUserId).Select(p => p.Person).ToList())).ToList());
    }
}

public record UpdateAppointmentRequest(DateTimeOffset StartTime, DateTimeOffset EndTime, Guid? AssignedToUserId);
public record AgendaVisit(Guid Id, Guid? WorkOrderId, string Type, DateTime StartTime, DateTime EndTime,
    string? Address, string? Notes, Guid? AssignedToUserId, string? WorkOrderCode, string? WorkOrderStatus,
    List<ScheduleParticipant> Participants);
