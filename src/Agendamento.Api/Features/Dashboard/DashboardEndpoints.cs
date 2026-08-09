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
            .RequireAuthorization("TenantPolicy");

        group.MapGet("summary", async ([AsParameters] GetDashboardSummaryRequest request, [FromServices] GetDashboardSummaryHandler handler, CancellationToken cancellationToken) =>
        {
            var query = new GetDashboardSummaryQuery { StartDate = request.StartDate, EndDate = request.EndDate };
            var result = await handler.HandleAsync(query, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetDashboardSummary")
        .Produces<DashboardSummaryDto>(StatusCodes.Status200OK);

        group.MapGet("schedule", async ([AsParameters] GetUpcomingScheduleRequest request, [FromServices] GetUpcomingScheduleHandler handler, CancellationToken cancellationToken) =>
        {
            var query = new GetUpcomingScheduleQuery { DaysAhead = request.DaysAhead ?? 7 };
            var result = await handler.HandleAsync(query, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetUpcomingSchedule")
        .Produces<List<UpcomingScheduleDto>>(StatusCodes.Status200OK);

        group.MapGet("report", async ([AsParameters] ExportPerformanceReportRequest request, [FromServices] ExportPerformanceReportHandler handler, CancellationToken cancellationToken) =>
        {
            var query = new ExportPerformanceReportQuery { StartDate = request.StartDate, EndDate = request.EndDate };
            var fileBytes = await handler.HandleAsync(query, cancellationToken);
            var fileName = $"performance_report_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
            return Results.File(fileBytes, "text/csv", fileName);
        })
        .WithName("ExportPerformanceReport")
        .Produces(StatusCodes.Status200OK, contentType: "text/csv");
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
