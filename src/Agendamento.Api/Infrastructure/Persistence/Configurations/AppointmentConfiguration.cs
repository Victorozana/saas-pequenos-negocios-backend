using Agendamento.Api.Domain.Appointments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendamento.Api.Infrastructure.Persistence.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.HasKey(a => a.Id);
        
        builder.HasQueryFilter(a => a.TenantId == Microsoft.EntityFrameworkCore.EF.Property<Guid>(a, "TenantId"));
        builder.HasIndex(a => a.TenantId);
        
        builder.Property(a => a.Address).HasMaxLength(255);
        builder.Property(a => a.Notes).HasMaxLength(1000);

        builder.HasOne(a => a.WorkOrder)
            .WithMany()
            .HasForeignKey(a => a.WorkOrderId)
            .OnDelete(DeleteBehavior.SetNull);
            
        // Index on start time for scheduling query performance
        builder.HasIndex(a => new { a.TenantId, a.StartTime });
        builder.HasIndex(a => new { a.TenantId, a.AssignedToUserId });
    }
}
