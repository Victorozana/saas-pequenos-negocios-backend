using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Agendamento.Domain.Tenants;
using Agendamento.Domain.Identity;

namespace Agendamento.Api.Infrastructure.Persistence.Configurations;

public class TenantMembershipConfiguration : IEntityTypeConfiguration<TenantMembership>
{
    public void Configure(EntityTypeBuilder<TenantMembership> builder)
    {
        builder.ToTable("tenant_memberships");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.TenantId).IsRequired();
        builder.Property(t => t.UserId).IsRequired();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(t => t.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => new { t.TenantId, t.UserId }).IsUnique();

        builder.Property(t => t.Role).HasMaxLength(50).IsRequired();
        builder.Property(t => t.IsLegalRepresentative).IsRequired();
    }
}
