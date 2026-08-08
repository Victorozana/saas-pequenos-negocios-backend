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
            CancellationToken cancellationToken) =>
        {
            var id = await handler.HandleAsync(command, cancellationToken);
            return Results.Created($"/api/v1/appointments/{id}", new { Id = id });
        })
        .WithName("CreateAppointment");

        group.MapGet("/", async (
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] Guid? assignedToUserId,
            [FromQuery] Guid? workOrderId,
            [FromServices] GetAppointmentsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetAppointmentsQuery
            {
                StartDate = startDate,
                EndDate = endDate,
                AssignedToUserId = assignedToUserId,
                WorkOrderId = workOrderId
            };
            var result = await handler.HandleAsync(query, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetAppointments");

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateAppointmentRequest request,
            [FromServices] UpdateAppointmentHandler handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateAppointmentCommand
            {
                Id = id,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                AssignedToUserId = request.AssignedToUserId
            };
            
            await handler.HandleAsync(command, cancellationToken);
            return Results.NoContent();
        })
        .WithName("UpdateAppointment");
    }
}

public class UpdateAppointmentRequest
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public Guid? AssignedToUserId { get; set; }
}
