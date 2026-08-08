using System;
using Agendamento.Api.Domain.Appointments;
using Agendamento.Api.Domain.Customers;
using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Domain.WorkOrders;
using FluentAssertions;
using Xunit;

namespace Agendamento.UnitTests.WorkOrders;

public class WorkOrderDomainTests
{
    [Fact]
    public void CreateFromQuotation_Should_CopyData_When_QuotationIsApproved()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var quotation = Quotation.Create(tenantId, "Q-123", customerId, DateTime.UtcNow, null, null, null);
        quotation.AddItem(QuotationItem.Create(Guid.NewGuid(), "Service 1", "Un", 1, 100, 0));
        quotation.MarkAsPending();
        quotation.Approve();

        // Act
        var workOrder = WorkOrder.CreateFromQuotation(quotation);

        // Assert
        workOrder.Should().NotBeNull();
        workOrder.TenantId.Should().Be(tenantId);
        workOrder.CustomerId.Should().Be(customerId);
        workOrder.QuotationId.Should().Be(quotation.Id);
        workOrder.Code.Should().Be("OS-Q-123");
        workOrder.Status.Should().Be(WorkOrderStatus.Scheduled);
        workOrder.Items.Should().HaveCount(1);
        workOrder.Items.First().ServiceName.Should().Be("Service 1");
    }

    [Fact]
    public void CreateFromQuotation_Should_ThrowException_When_QuotationIsNotApproved()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var quotation = Quotation.Create(tenantId, "Q-123", Guid.NewGuid(), DateTime.UtcNow, null, null, null);

        // Act & Assert
        Action act = () => WorkOrder.CreateFromQuotation(quotation);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Can only create a work order from an approved quotation.");
    }
    
    [Fact]
    public void Appointment_Should_ThrowException_When_EndTimeIsBeforeStartTime()
    {
        // Act & Assert
        Action act = () => Appointment.Create(Guid.NewGuid(), null, AppointmentType.TechnicalVisit, DateTime.UtcNow.AddHours(2), DateTime.UtcNow.AddHours(1), null, null, null);
        act.Should().Throw<ArgumentException>()
            .WithMessage("End time must be after start time. (Parameter 'endTime')");
    }
}
