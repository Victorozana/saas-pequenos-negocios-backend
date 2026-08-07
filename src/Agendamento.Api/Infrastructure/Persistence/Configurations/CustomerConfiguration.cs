using Agendamento.Api.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendamento.Api.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(c => c.Id);
        
        // Isolamento Multi-Tenancy (Row Level Security / EF Query Filter)
        builder.Property(c => c.TenantId).IsRequired();
        builder.HasIndex(c => new { c.TenantId, c.Id });

        builder.Property(c => c.Name).HasMaxLength(250).IsRequired();
        builder.Property(c => c.Phone).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(150);

        builder.OwnsOne(c => c.Document, doc =>
        {
            doc.Property(d => d.Type).HasMaxLength(10).HasColumnName("DocumentType");
            doc.Property(d => d.Value).HasMaxLength(20).HasColumnName("DocumentValue");
            // Índice para busca por documento por tenant
            doc.HasIndex(d => new { d.Value });
        });

        builder.OwnsOne(c => c.Address, address =>
        {
            address.Property(a => a.Street).HasMaxLength(200).HasColumnName("Street");
            address.Property(a => a.Number).HasMaxLength(20).HasColumnName("Number");
            address.Property(a => a.Complement).HasMaxLength(100).HasColumnName("Complement");
            address.Property(a => a.Neighborhood).HasMaxLength(100).HasColumnName("Neighborhood");
            address.Property(a => a.City).HasMaxLength(100).HasColumnName("City");
            address.Property(a => a.State).HasMaxLength(2).HasColumnName("State");
            address.Property(a => a.ZipCode).HasMaxLength(15).HasColumnName("ZipCode");
        });

        // Índices para busca
        builder.HasIndex(c => new { c.TenantId, c.Name });
        builder.HasIndex(c => new { c.TenantId, c.Phone });
    }
}
