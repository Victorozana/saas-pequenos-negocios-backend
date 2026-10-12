using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Application.WorkOrders.ConvertQuotationToWorkOrder;
using Agendamento.Api.Application.Appointments.CreateAppointment;
using Agendamento.Api.Domain.Appointments;
using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Agendamento.IntegrationTests.WorkOrders;

public class WorkOrderPersistenceTests
{
    private sealed class TenantContext(Guid tenantId) : ITenantContext
    {
        public Guid TenantId => tenantId;
        public bool HasTenant => true;
    }

    [Fact]
    public async Task ConvertedOrder_IsPersisted_Reusable_AndCanBeScheduled()
    {
        var tenant = new TenantContext(Guid.NewGuid());
        var options = new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        Guid quotationId;
        using (var setup = new AgendamentoDbContext(options, tenant))
        {
            var quotation = Quotation.Create(tenant.TenantId, "Q-PERSIST", Guid.NewGuid(), DateTime.UtcNow, null, null, null);
            quotation.AddItem(QuotationItem.Create(null, "Instalacao", "un", 1, 100, 0));
            quotation.MarkAsPending();
            quotation.Approve();
            setup.Quotations.Add(quotation);
            await setup.SaveChangesAsync();
            quotationId = quotation.Id;
        }
        Guid orderId;
        using (var conversion = new AgendamentoDbContext(options, tenant))
        {
            var handler = new ConvertQuotationToWorkOrderHandler(conversion, tenant, new UnitOfWork(conversion));
            orderId = await handler.HandleAsync(new() { QuotationId = quotationId });
        }
        using var verification = new AgendamentoDbContext(options, tenant);
        Assert.Equal(orderId, (await verification.WorkOrders.SingleAsync()).Id);
        Assert.Equal(QuotationStatus.Converted, (await verification.Quotations.SingleAsync()).Status);
        var repeat = new ConvertQuotationToWorkOrderHandler(verification, tenant, new UnitOfWork(verification));
        Assert.Equal(orderId, await repeat.HandleAsync(new() { QuotationId = quotationId }));
        var scheduling = new CreateAppointmentHandler(verification, tenant, new UnitOfWork(verification));
        await scheduling.HandleAsync(new CreateAppointmentCommand {
            WorkOrderId = orderId, Type = AppointmentType.Installation,
            StartTime = DateTime.UtcNow.AddDays(1), EndTime = DateTime.UtcNow.AddDays(1).AddHours(1)
        });
        Assert.Equal(orderId, (await verification.Appointments.SingleAsync()).WorkOrderId);
        using var otherTenant = new AgendamentoDbContext(options, new TenantContext(Guid.NewGuid()));
        var forbidden = new ConvertQuotationToWorkOrderHandler(otherTenant, new TenantContext(Guid.NewGuid()), new UnitOfWork(otherTenant));
        await Assert.ThrowsAsync<InvalidOperationException>(() => forbidden.HandleAsync(new() { QuotationId = quotationId }));
    }
}
