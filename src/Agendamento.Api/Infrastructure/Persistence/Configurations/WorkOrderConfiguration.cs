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
        builder.HasIndex(wo => new { wo.TenantId, wo.QuotationId }).IsUnique();

        builder.Property(wo => wo.Code).IsRequired().HasMaxLength(53);
        builder.Property(wo => wo.Notes).HasMaxLength(2000);

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
            itemBuilder.Property<Guid>("TenantId").HasColumnName("tenant_id").IsRequired();
            itemBuilder.HasIndex("TenantId");
            itemBuilder.HasOne<Agendamento.Domain.Tenants.Tenant>().WithMany().HasForeignKey("TenantId").OnDelete(DeleteBehavior.Restrict);
            itemBuilder.HasOne<Agendamento.Api.Domain.Services.ServiceItem>().WithMany()
                .HasForeignKey("TenantId", nameof(WorkOrderItem.ServiceItemId))
                .HasPrincipalKey(nameof(Agendamento.Api.Domain.Services.ServiceItem.TenantId), "Id")
                .OnDelete(DeleteBehavior.Restrict);
            itemBuilder.Property(i => i.ServiceName).IsRequired();
            itemBuilder.Property(i => i.Unit).IsRequired();
            itemBuilder.Property(i => i.Quantity).HasPrecision(18, 2);
            itemBuilder.Property(i => i.UnitPrice).HasPrecision(18, 2);
            itemBuilder.Property(i => i.DiscountAmount).HasPrecision(18, 2);
            itemBuilder.WithOwner().HasForeignKey(i => i.WorkOrderId);
        });

        builder.Metadata.FindNavigation(nameof(WorkOrder.Items))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
