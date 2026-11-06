using Microsoft.EntityFrameworkCore;
using Agendamento.Api.Infrastructure.Authorization;
using Agendamento.Api.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Agendamento.Api.Application.Dashboard.GetDashboardSummary;
using Agendamento.Api.Application.Dashboard.GetUpcomingSchedule;
using Agendamento.Api.Application.Dashboard.ExportPerformanceReport;

namespace Agendamento.Api.Features.Dashboard;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("api/v1/dashboard")
            .WithTags("Dashboard")
            .RequireAuthorization();

        group.MapGet("summary", async ([AsParameters] GetDashboardSummaryRequest request, [FromServices] GetDashboardSummaryHandler handler, CancellationToken cancellationToken) =>
        {
            var query = new GetDashboardSummaryQuery { StartDate = request.StartDate, EndDate = request.EndDate };
            var result = await handler.HandleAsync(query, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetDashboardSummary")
        .RequirePermission("sales.read")
        .Produces<DashboardSummaryDto>(StatusCodes.Status200OK);

        group.MapGet("schedule", async ([AsParameters] GetUpcomingScheduleRequest request,
            System.Security.Claims.ClaimsPrincipal principal, Agendamento.Api.Features.WorkOrders.WorkOrderSchedule schedule,
            TimeProvider clock, CancellationToken ct) =>
        {
            var (start, end) = Agendamento.Api.Features.Appointments.AppointmentEndpoints.DayRange(clock.GetUtcNow(),
                Math.Clamp(request.DaysAhead ?? 7, 0, 365) + 1);

            var actor = await schedule.ActorAsync(principal, ct);
            if (actor == null || !actor.Membership.HasPermission("appointments.read")) return Results.Forbid();
            var visits = await schedule.VisibleAppointments(actor)
                .Where(a => a.StartTime < end && a.EndTime > start).OrderBy(a => a.StartTime)
                .Select(a => new UpcomingScheduleDto
                {
                    AppointmentId = a.Id,
                    Type = a.Type,
                    StartTime = a.StartTime,
                    EndTime = a.EndTime,
                    Address = a.Address,
                    Notes = a.Notes,
                    WorkOrderCode = a.WorkOrder == null ? null : a.WorkOrder.Code,
                    CustomerName = a.WorkOrder != null && a.WorkOrder.Customer != null ? a.WorkOrder.Customer.Name : null
                }).ToListAsync(ct);
            return Results.Ok(visits);
        }).WithName("GetUpcomingSchedule").RequirePermission("appointments.read")
            .Produces<List<UpcomingScheduleDto>>();

        group.MapGet("report", async ([AsParameters] ExportPerformanceReportRequest request, [FromServices] ExportPerformanceReportHandler handler, CancellationToken cancellationToken) =>
        {
            var query = new ExportPerformanceReportQuery { StartDate = request.StartDate, EndDate = request.EndDate };
            var fileBytes = await handler.HandleAsync(query, cancellationToken);
            var fileName = $"performance_report_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
            return Results.File(fileBytes, "text/csv", fileName);
        })
        .WithName("ExportPerformanceReport")
        .RequirePermission("sales.read")
        .Produces(StatusCodes.Status200OK, contentType: "text/csv")
        .DocumentDownload("text/csv", false);
    }
}

public class GetDashboardSummaryRequest
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class GetUpcomingScheduleRequest
{
    public int? DaysAhead { get; set; }
}

public class ExportPerformanceReportRequest
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
