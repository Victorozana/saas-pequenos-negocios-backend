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
            CancellationToken cancellationToken) =>
        {
            try
            {
                var id = await handler.HandleAsync(command, cancellationToken);
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
        .Produces<CreatedIdResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

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
        .WithName("GetAppointments")
        .Produces<List<AppointmentResponse>>()
        .RequirePermission("appointments.read");

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
            
            try
            {
                await handler.HandleAsync(command, cancellationToken);
                return Results.NoContent();
            }
            catch (ArgumentException)
            {
                return Results.Problem(statusCode: 400, title: "O horário final deve ser posterior ao inicial.");
            }
            catch (InvalidOperationException ex) when (ex.Message == "Appointment not found.")
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("Scheduling conflict:", StringComparison.Ordinal))
            {
                return Results.Problem(statusCode: 400, title: "O profissional já possui agendamento neste período.");
            }
        })
        .WithName("UpdateAppointment")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .RequirePermission("appointments.write");
    }
}

public class UpdateAppointmentRequest
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public Guid? AssignedToUserId { get; set; }
}
