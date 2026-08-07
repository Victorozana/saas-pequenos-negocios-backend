using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Agendamento.Domain.Tenants;

namespace Agendamento.Api.Infrastructure.Persistence.Configurations;

public class TenantFiscalProfileConfiguration : IEntityTypeConfiguration<TenantFiscalProfile>
{
    public void Configure(EntityTypeBuilder<TenantFiscalProfile> builder)
    {
        builder.ToTable("tenant_fiscal_profiles");
        builder.HasKey(t => t.TenantId);
        builder.Property(t => t.TenantId).ValueGeneratedNever();
        
        builder.HasOne<Tenant>()
            .WithOne()
            .HasForeignKey<TenantFiscalProfile>(t => t.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(t => t.StateRegistration).HasMaxLength(50);
        builder.Property(t => t.MunicipalRegistration).HasMaxLength(50);
        builder.Property(t => t.IsTaxExempt).IsRequired();
        builder.Property(t => t.TaxRegime).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(t => t.FiscalEmail).HasMaxLength(255).IsRequired();
    }
}
