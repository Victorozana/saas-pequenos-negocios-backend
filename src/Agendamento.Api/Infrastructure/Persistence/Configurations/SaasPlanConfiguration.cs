using Agendamento.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendamento.Infrastructure.Persistence.Configurations;

internal sealed class SaasPlanConfiguration : IEntityTypeConfiguration<SaasPlan>
{
    public void Configure(EntityTypeBuilder<SaasPlan> builder)
    {
        builder.ToTable("SaasPlans");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.BillingCycle)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.Price)
            .HasPrecision(18, 2);

        builder.OwnsOne(x => x.Limits, limits =>
        {
            limits.Property(l => l.MaxUsers)
                .HasColumnName("MaxUsers")
                .IsRequired();

            limits.Property(l => l.MaxCustomers)
                .HasColumnName("MaxCustomers")
                .IsRequired();

            limits.Property(l => l.MaxQuotationsPerMonth)
                .HasColumnName("MaxQuotationsPerMonth")
                .IsRequired();
        });
    }
}
