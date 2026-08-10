using System;
using System.Linq;
using Agendamento.Api.Domain.Appointments;
using Agendamento.Api.Domain.Customers;
using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Domain.WorkOrders;
using Xunit;

namespace Agendamento.UnitTests.WorkOrders;

public class WorkOrderDomainTests
{
    [Fact(DisplayName = "CreateFromQuotation copia os dados do orçamento quando está aprovado @spec:AC-051 @spec:AC-052")]
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
        Assert.NotNull(workOrder);
        Assert.Equal(tenantId, workOrder.TenantId);
        Assert.Equal(customerId, workOrder.CustomerId);
        Assert.Equal(quotation.Id, workOrder.QuotationId);
        Assert.Equal("OS-Q-123", workOrder.Code);
        Assert.Equal(WorkOrderStatus.Scheduled, workOrder.Status);
        Assert.Single(workOrder.Items);
        Assert.Equal("Service 1", workOrder.Items.First().ServiceName);
    }

    [Fact(DisplayName = "CreateFromQuotation lança exceção quando o orçamento não está aprovado @spec:AC-051")]
    public void CreateFromQuotation_Should_ThrowException_When_QuotationIsNotApproved()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var quotation = Quotation.Create(tenantId, "Q-123", Guid.NewGuid(), DateTime.UtcNow, null, null, null);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => WorkOrder.CreateFromQuotation(quotation));
        Assert.Equal("Can only create a work order from an approved quotation.", ex.Message);
    }
    
    [Fact(DisplayName = "Appointment lança exceção quando EndTime é anterior a StartTime @spec:AC-050 @spec:AC-053")]
    public void Appointment_Should_ThrowException_When_EndTimeIsBeforeStartTime()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => Appointment.Create(Guid.NewGuid(), null, AppointmentType.TechnicalVisit, DateTime.UtcNow.AddHours(2), DateTime.UtcNow.AddHours(1), null, null, null));
        Assert.Equal("End time must be after start time. (Parameter 'endTime')", ex.Message);
    }
}
