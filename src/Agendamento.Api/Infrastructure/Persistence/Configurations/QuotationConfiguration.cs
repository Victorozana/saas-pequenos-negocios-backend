using Agendamento.Api.Domain.Quotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendamento.Api.Infrastructure.Persistence.Configurations;

public class QuotationConfiguration : IEntityTypeConfiguration<Quotation>
{
    public void Configure(EntityTypeBuilder<Quotation> builder)
    {
        builder.ToTable("Quotations");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(q => q.Notes)
            .HasMaxLength(2000);

        builder.Property(q => q.PaymentTerms)
            .HasMaxLength(2000);

        builder.Property(q => q.SubtotalAmount).HasPrecision(18, 2);
        builder.Property(q => q.DiscountAmount).HasPrecision(18, 2);
        builder.Property(q => q.TotalAmount).HasPrecision(18, 2);

        builder.Property(q => q.TenantId).IsRequired();

        // Foreign Keys
        builder.HasOne(q => q.Customer)
            .WithMany()
            .HasForeignKey(q => q.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Owned Type for DepositInfo
        builder.OwnsOne(q => q.DepositInfo, d =>
        {
            d.Property(di => di.Type).HasColumnName("DepositType");
            d.Property(di => di.Value).HasColumnName("DepositValue").HasPrecision(18, 2);
            d.Property(di => di.RequiredAmount).HasColumnName("DepositRequiredAmount").HasPrecision(18, 2);
            d.Property(di => di.RemainingBalance).HasColumnName("DepositRemainingBalance").HasPrecision(18, 2);
            d.Property(di => di.PaymentNotes).HasColumnName("DepositPaymentNotes").HasMaxLength(1000);
            d.Property(di => di.IsPaid).HasColumnName("DepositIsPaid");
        });

        // Navigation collection
        builder.HasMany(q => q.Items)
            .WithOne()
            .HasForeignKey(i => i.QuotationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Isolamento Multitenant
        builder.HasQueryFilter(q => EF.Property<Guid>(q, "TenantId") == q.TenantId);

        // Índices
        builder.HasIndex(q => new { q.TenantId, q.Code }).IsUnique();
        builder.HasIndex(q => new { q.TenantId, q.Status });
        builder.HasIndex(q => new { q.TenantId, q.CustomerId });
    }
}
