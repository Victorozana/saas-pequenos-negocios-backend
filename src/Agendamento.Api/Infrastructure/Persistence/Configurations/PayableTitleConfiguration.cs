using Agendamento.Api.Domain.Financial;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendamento.Api.Infrastructure.Persistence.Configurations;

public class PayableTitleConfiguration : IEntityTypeConfiguration<PayableTitle>
{
    public void Configure(EntityTypeBuilder<PayableTitle> builder)
    {
        builder.ToTable("PayableTitles");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.SupplierName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Description)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(p => p.OriginalAmount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(p => p.DueDate)
            .IsRequired();

        builder.OwnsMany(p => p.Payments, pb =>
        {
            pb.ToTable("PayablePayments");
            pb.HasKey(pay => pay.Id);
            pb.Property(pay => pay.Amount).HasColumnType("decimal(18,2)").IsRequired();
            pb.Property(pay => pay.PaymentDate).IsRequired();
            pb.Property(pay => pay.Method).HasConversion<string>().IsRequired();
            pb.Property(pay => pay.Notes).HasMaxLength(500);
            pb.WithOwner().HasForeignKey("PayableTitleId");
        });

        builder.HasIndex(p => new { p.TenantId, p.DueDate, p.Status });
    }
}
