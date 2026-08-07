using Agendamento.Api.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendamento.Api.Infrastructure.Persistence.Configurations;

public class ServiceItemConfiguration : IEntityTypeConfiguration<ServiceItem>
{
    public void Configure(EntityTypeBuilder<ServiceItem> builder)
    {
        builder.ToTable("ServiceItems");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Description)
            .HasMaxLength(1000);

        builder.Property(s => s.Unit)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.BasePrice)
            .HasPrecision(18, 2);

        builder.Property(s => s.TenantId)
            .IsRequired();

        // Isolamento Multitenant (Global Query Filter)
        builder.HasQueryFilter(s => EF.Property<Guid>(s, "TenantId") == s.TenantId);
        
        // Indices para performance em listagem de catálogo
        builder.HasIndex(s => new { s.TenantId, s.IsActive, s.Name });
    }
}
