using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Agendamento.Domain.Tenants;

namespace Agendamento.Api.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Cnpj)
            .HasConversion(c => c.Value, v => Cnpj.Create(v))
            .HasMaxLength(14)
            .IsRequired();
        
        builder.HasIndex(t => t.Cnpj).IsUnique(); // AC-022

        builder.Property(t => t.CompanyName).HasMaxLength(200).IsRequired();
        builder.Property(t => t.TradeName).HasMaxLength(200);
        builder.Property(t => t.LegalNature).HasMaxLength(100);
        builder.Property(t => t.PrimaryCnae).HasMaxLength(20).IsRequired();
        builder.Property(t => t.Category).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(t => t.Email).HasMaxLength(255).IsRequired();
        builder.Property(t => t.Phone).HasMaxLength(20).IsRequired();
        builder.Property(t => t.OnboardingStatus).HasMaxLength(50).IsRequired();
        builder.Property(t => t.CreatedAtUtc).IsRequired();

        builder.ComplexProperty(t => t.Address, a =>
        {
            a.IsRequired();
            a.Property(p => p.Street).HasColumnName("address_street").HasMaxLength(150);
            a.Property(p => p.Number).HasColumnName("address_number").HasMaxLength(20);
            a.Property(p => p.Complement).HasColumnName("address_complement").HasMaxLength(100);
            a.Property(p => p.Neighborhood).HasColumnName("address_neighborhood").HasMaxLength(100);
            a.Property(p => p.City).HasColumnName("address_city").HasMaxLength(100);
            a.Property(p => p.State).HasColumnName("address_state").HasMaxLength(2);
            a.Property(p => p.ZipCode).HasColumnName("address_zip_code").HasMaxLength(20);
        });
    }
}
