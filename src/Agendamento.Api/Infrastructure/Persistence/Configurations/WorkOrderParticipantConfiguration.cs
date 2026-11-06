using Agendamento.Api.Domain.WorkOrders;
using Agendamento.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendamento.Api.Infrastructure.Persistence.Configurations;

public sealed class WorkOrderParticipantConfiguration : IEntityTypeConfiguration<WorkOrderParticipant>
{
    public void Configure(EntityTypeBuilder<WorkOrderParticipant> builder)
    {
        builder.HasKey(p => new { p.TenantId, p.WorkOrderId, p.UserId });
        builder.HasOne<WorkOrder>().WithMany()
            .HasForeignKey(p => new { p.TenantId, p.WorkOrderId })
            .HasPrincipalKey(w => new { w.TenantId, w.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TenantMembership>().WithMany()
            .HasForeignKey(p => new { p.TenantId, p.UserId })
            .HasPrincipalKey(m => new { m.TenantId, m.UserId }).OnDelete(DeleteBehavior.Restrict);
    }
}
