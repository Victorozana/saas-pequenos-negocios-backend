using Agendamento.Api.Domain.Financial;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendamento.Api.Infrastructure.Persistence.Configurations;

public class ReceivableTitleConfiguration : IEntityTypeConfiguration<ReceivableTitle>
{
    public void Configure(EntityTypeBuilder<ReceivableTitle> builder)
    {
        builder.ToTable("ReceivableTitles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Description)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(r => r.OriginalAmount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(r => r.DueDate)
            .IsRequired();

        builder.OwnsMany(r => r.Payments, pb =>
        {
            pb.ToTable("ReceivablePayments");
            pb.HasKey(p => p.Id);
            pb.Property(p => p.Amount).HasColumnType("decimal(18,2)").IsRequired();
            pb.Property(p => p.PaymentDate).IsRequired();
            pb.Property(p => p.Method).HasConversion<string>().IsRequired();
            pb.Property(p => p.Notes).HasMaxLength(500);
            pb.WithOwner().HasForeignKey("ReceivableTitleId");
        });
        
        builder.HasOne(r => r.Customer)
            .WithMany()
            .HasForeignKey(r => r.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.HasOne(r => r.Quotation)
            .WithMany()
            .HasForeignKey(r => r.QuotationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(r => new { r.TenantId, r.DueDate, r.Status });
    }
}
