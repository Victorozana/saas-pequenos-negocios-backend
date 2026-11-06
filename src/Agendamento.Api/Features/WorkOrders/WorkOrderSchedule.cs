using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Appointments;
using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Domain.WorkOrders;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.Domain.Identity;
using Agendamento.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Features.WorkOrders;

public record ScheduleWorkOrderRequest(AppointmentType Type, DateTimeOffset StartTime, DateTimeOffset EndTime,
    Guid[] ParticipantIds, string? Address, string? Notes);
public sealed class ScheduleParticipant
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required bool IsActive { get; init; }
}
public record ScheduledWorkOrder(Guid Id, string Code, Guid QuotationId, string Status,
    List<ScheduleParticipant> Participants, ScheduledVisit? Visit, bool NeedsRegularization);
public record VisitParticipant(Guid? WorkOrderId, ScheduleParticipant Person);
public record ScheduledVisit(Guid Id, AppointmentType Type, DateTime StartTime, DateTime EndTime, string? Address, string? Notes);
public record WorkOrderPage(List<ScheduledWorkOrder> Items, int Page, int PageSize, int Total);
public record ScheduleActor(Guid Id, TenantMembership Membership)
{
    public bool IsAdministrator => TenantMembership.IsAdministrativeRole(Membership.Role);
    public bool CanManage => IsAdministrator && Membership.HasPermission(Permissions.SalesWrite)
        && Membership.HasPermission(Permissions.AppointmentsWrite);
}

public sealed class WorkOrderSchedule(AgendamentoDbContext db, ITenantContext tenant)
{
    public async Task<ScheduleActor?> ActorAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        if (!tenant.HasTenant || !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            return null;
        var membership = await db.TenantMemberships.AsNoTracking()
            .SingleOrDefaultAsync(m => m.TenantId == tenant.TenantId && m.UserId == id && m.IsActive, ct);
        return membership != null && await db.Users.AnyAsync(u => u.Id == id && u.Status == UserStatus.Active, ct)
            ? new ScheduleActor(id, membership) : null;
    }

    public IQueryable<WorkOrder> VisibleOrders(ScheduleActor actor) => db.WorkOrders.AsNoTracking().Where(w =>
        w.TenantId == tenant.TenantId && (actor.IsAdministrator ||
            db.WorkOrderParticipants.Any(p => p.WorkOrderId == w.Id && p.UserId == actor.Id)));

    public IQueryable<Appointment> VisibleAppointments(ScheduleActor actor) => db.Appointments.AsNoTracking()
        .Where(a => a.TenantId == tenant.TenantId && (actor.IsAdministrator ||
            (a.WorkOrderId == null ? a.AssignedToUserId == actor.Id :
                db.WorkOrderParticipants.Any(p => p.WorkOrderId == a.WorkOrderId && p.UserId == actor.Id))));

    public IQueryable<ScheduleParticipant> Members() =>
        from m in db.TenantMemberships
        join u in db.Users on m.UserId equals u.Id
        where m.TenantId == tenant.TenantId
        select new ScheduleParticipant { Id = u.Id, Name = u.Name, IsActive = m.IsActive && u.Status == UserStatus.Active };

    public IQueryable<Guid> ParticipantOrders(Guid id) =>
        db.WorkOrderParticipants.Where(p => p.UserId == id).Select(p => p.WorkOrderId);


    public async Task<List<VisitParticipant>> AppointmentParticipants(Guid[] orders, Guid[] standalone, CancellationToken ct)
    {
        var linked = await (from p in db.WorkOrderParticipants
            join person in Members() on p.UserId equals person.Id
            where Enumerable.Contains(orders, p.WorkOrderId)
            select new VisitParticipant(p.WorkOrderId, person)).ToListAsync(ct);
        linked.AddRange(await Members().Where(m => Enumerable.Contains(standalone, m.Id))
            .Select(m => new VisitParticipant(null, m)).ToListAsync(ct));
        return linked;
    }

    public async Task<ScheduledWorkOrder> DescribeAsync(WorkOrder order, CancellationToken ct)
    {
        var participants = await Members().Where(m => db.WorkOrderParticipants.Any(p => p.UserId == m.Id && p.WorkOrderId == order.Id))
            .OrderBy(m => m.Name).ToListAsync(ct);
        var visit = await db.Appointments.Where(a => a.WorkOrderId == order.Id).OrderBy(a => a.StartTime)
            .Select(a => new ScheduledVisit(a.Id, a.Type, a.StartTime, a.EndTime, a.Address, a.Notes))
            .FirstOrDefaultAsync(ct);
        return new(order.Id, order.Code, order.QuotationId, order.Status.ToString(), participants, visit,
            participants.Count == 0 || participants.Any(p => !p.IsActive) || visit == null);
    }

    public async Task LockAsync(CancellationToken ct)
    {
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({tenant.TenantId.ToString()}, 0))", ct);
    }

    public async Task<bool> HasConflictAsync(Guid[] participants, DateTime start, DateTime end,
        Guid? excludedAppointment, CancellationToken ct)
    {
        return await db.Appointments.AnyAsync(a => a.Id != excludedAppointment
            && a.StartTime < end && a.EndTime > start
            && (a.WorkOrderId == null
                ? a.AssignedToUserId.HasValue && Enumerable.Contains(participants, a.AssignedToUserId.Value)
                : db.WorkOrders.Any(w => w.Id == a.WorkOrderId && w.Status != WorkOrderStatus.Cancelled
                    && w.Status != WorkOrderStatus.Completed)
                    && db.WorkOrderParticipants.Any(p => p.WorkOrderId == a.WorkOrderId && Enumerable.Contains(participants, p.UserId))), ct);
    }

    public async Task<IResult> SaveAsync(ClaimsPrincipal principal, Guid sourceId, Guid? requestKey,
        ScheduleWorkOrderRequest request, bool create, bool quotationAlias, CancellationToken ct)
    {
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        await LockAsync(ct);
        var actor = await ActorAsync(principal, ct);
        if (actor?.CanManage != true) return Results.Problem(statusCode: 403, title: "Apenas administradores podem distribuir ordens de serviço.");

        var ids = request.ParticipantIds?.Order().ToArray() ?? [];
        var start = request.StartTime.UtcDateTime;
        var end = request.EndTime.UtcDateTime;
        if (ids.Length == 0 || ids.Distinct().Count() != ids.Length || start == default || end <= start
            || !Enum.IsDefined(request.Type) || request.Address?.Length > 255 || request.Notes?.Length > 1000
            || (create && (!requestKey.HasValue || requestKey == Guid.Empty)))
            return Results.Problem(statusCode: 400, title: "Informe equipe, atividade e horário válidos.");

        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            new { sourceId, ids, request.Type, start, end, request.Address, request.Notes })));
        if (create)
        {
            var replay = await db.WorkOrders.AsNoTracking().SingleOrDefaultAsync(w => w.RequestKey == requestKey, ct);
            if (replay != null)
                return replay.RequestHash == hash ? Created(replay.Id, quotationAlias)
                    : Results.Problem(statusCode: 409, title: "Este identificador já foi usado em outra solicitação.");
        }

        if (await Members().CountAsync(m => m.IsActive && Enumerable.Contains(ids, m.Id), ct) != ids.Length)
            return Results.Problem(statusCode: 400, title: "Selecione apenas funcionários ativos desta empresa.");

        WorkOrder order;
        List<Appointment> visits;
        if (create)
        {
            if (db.Database.IsNpgsql())
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM \"Quotations\" WHERE \"Id\" = {sourceId} AND tenant_id = {tenant.TenantId} FOR UPDATE", ct);
            var quotation = await db.Quotations.Include(q => q.Items).SingleOrDefaultAsync(q => q.Id == sourceId, ct);
            if (quotation == null) return Results.NotFound();
            if (quotation.Status is not (QuotationStatus.Approved or QuotationStatus.Converted))
                return Results.Problem(statusCode: 400, title: "Aprove o orçamento antes de criar a ordem de serviço.");

            order = WorkOrder.CreateFromQuotation(quotation);
            var sequence = await db.WorkOrders.MaxAsync(w => w.Sequence, ct) ?? 0;
            string code;
            do { code = $"OS-{++sequence:D8}"; }
            while (await db.WorkOrders.AnyAsync(w => w.Code == code, ct));
            order.RecordScheduleCreation(requestKey!.Value, hash, sequence);
            if (quotation.Status == QuotationStatus.Approved) quotation.MarkAsConverted();
            db.WorkOrders.Add(order);
            visits = [];
        }
        else
        {
            var existing = await db.WorkOrders.SingleOrDefaultAsync(w => w.Id == sourceId, ct);
            if (existing == null) return Results.NotFound();
            if (existing.Status is WorkOrderStatus.Completed or WorkOrderStatus.Cancelled)
                return Results.Problem(statusCode: 409, title: "A ordem já foi encerrada.");
            order = existing;
            visits = await db.Appointments.Where(a => a.WorkOrderId == order.Id).OrderBy(a => a.StartTime).ToListAsync(ct);
        }

        var visit = visits.FirstOrDefault();
        if (await HasConflictAsync(ids, start, end, visit?.Id, ct))
            return Results.Problem(statusCode: 409, title: "Um participante já tem compromisso neste horário.");
        foreach (var other in visits.Skip(1).Where(a => a.EndTime > DateTime.UtcNow))
        {
            if ((other.StartTime < end && other.EndTime > start)
                || await HasConflictAsync(ids, other.StartTime, other.EndTime, other.Id, ct))
                return Results.Problem(statusCode: 409, title: "Um participante tem conflito em outro horário desta OS.");
        }

        if (visit == null)
            db.Appointments.Add(Appointment.Create(tenant.TenantId, order.Id, request.Type, start, end, request.Address, request.Notes, null));
        else
            visit.UpdateWorkOrderVisit(request.Type, start, end, request.Address, request.Notes);

        var current = create ? [] : await db.WorkOrderParticipants.Where(p => p.WorkOrderId == order.Id).ToListAsync(ct);
        db.WorkOrderParticipants.RemoveRange(current.Where(p => !ids.Contains(p.UserId)));
        db.WorkOrderParticipants.AddRange(ids.Except(current.Select(p => p.UserId)).Select(id =>
            new WorkOrderParticipant { TenantId = tenant.TenantId, WorkOrderId = order.Id, UserId = id }));
        await db.SaveChangesAsync(ct);
        if (transaction != null) await transaction.CommitAsync(ct);
        return create ? Created(order.Id, quotationAlias) : Results.NoContent();
    }

    private static IResult Created(Guid id, bool quotationAlias) => quotationAlias
        ? Results.Ok(new Agendamento.Api.Features.Quotations.WorkOrderConversionResponse(id))
        : Results.Created($"/api/v1/work-orders/{id}", new Agendamento.Api.Features.Common.CreatedIdResponse(id));
}
