using Agendamento.Api.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendamento.Api.Infrastructure.Persistence.Configurations;

public class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.HasKey(wo => wo.Id);
        
        builder.HasQueryFilter(wo => wo.TenantId == Microsoft.EntityFrameworkCore.EF.Property<Guid>(wo, "TenantId"));
        builder.HasIndex(wo => wo.TenantId);

        builder.Property(wo => wo.Code).IsRequired().HasMaxLength(50);
        builder.Property(wo => wo.Notes).HasMaxLength(1000);

        builder.HasOne(wo => wo.Customer)
            .WithMany()
            .HasForeignKey(wo => wo.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(wo => wo.Quotation)
            .WithMany()
            .HasForeignKey(wo => wo.QuotationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsMany(wo => wo.Items, itemBuilder =>
        {
            itemBuilder.ToTable("WorkOrderItems");
            itemBuilder.HasKey(i => i.Id);
            itemBuilder.Property(i => i.ServiceName).IsRequired().HasMaxLength(255);
            itemBuilder.Property(i => i.Unit).HasMaxLength(50);
            itemBuilder.Property(i => i.Quantity).HasPrecision(18, 2);
            itemBuilder.Property(i => i.UnitPrice).HasPrecision(18, 2);
            itemBuilder.Property(i => i.DiscountAmount).HasPrecision(18, 2);
            itemBuilder.WithOwner().HasForeignKey(i => i.WorkOrderId);
        });

        builder.Metadata.FindNavigation(nameof(WorkOrder.Items))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
