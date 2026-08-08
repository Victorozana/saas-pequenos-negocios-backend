using Agendamento.Domain.Common;
using Agendamento.Api.Domain.WorkOrders;

namespace Agendamento.Api.Domain.Appointments;

public enum AppointmentType
{
    TechnicalVisit = 1,
    Measurement = 2,
    Installation = 3,
    Other = 4
}

public class Appointment : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; set; }
    
    public Guid? WorkOrderId { get; private set; }
    public WorkOrder? WorkOrder { get; private set; }
    
    public AppointmentType Type { get; private set; }
    public DateTime StartTime { get; private set; }
    public DateTime EndTime { get; private set; }
    
    public string? Address { get; private set; }
    public string? Notes { get; private set; }
    
    // Opcionalmente vincula a um técnico/usuário do sistema
    public Guid? AssignedToUserId { get; private set; }

    private Appointment() { } // EF Core

    public static Appointment Create(Guid tenantId, Guid? workOrderId, AppointmentType type, DateTime startTime, DateTime endTime, string? address, string? notes, Guid? assignedToUserId)
    {
        if (endTime <= startTime)
            throw new ArgumentException("End time must be after start time.", nameof(endTime));

        return new Appointment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkOrderId = workOrderId,
            Type = type,
            StartTime = startTime,
            EndTime = endTime,
            Address = address,
            Notes = notes,
            AssignedToUserId = assignedToUserId
        };
    }

    public void Reschedule(DateTime newStartTime, DateTime newEndTime)
    {
        if (newEndTime <= newStartTime)
            throw new ArgumentException("End time must be after start time.", nameof(newEndTime));

        StartTime = newStartTime;
        EndTime = newEndTime;
    }
    
    public void AssignTo(Guid userId)
    {
        AssignedToUserId = userId;
    }
}
