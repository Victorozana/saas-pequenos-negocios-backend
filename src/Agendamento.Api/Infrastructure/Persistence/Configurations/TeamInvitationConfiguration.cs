using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Agendamento.Domain.Tenants;

namespace Agendamento.Api.Infrastructure.Persistence.Configurations;

public class TeamInvitationConfiguration : IEntityTypeConfiguration<TeamInvitation>
{
    public void Configure(EntityTypeBuilder<TeamInvitation> builder)
    {
        builder.ToTable("team_invitations");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.TenantId).IsRequired();
        builder.Property(t => t.Email).HasMaxLength(255).IsRequired();
        builder.Property(t => t.Role).HasMaxLength(50).IsRequired();
        builder.Property(t => t.Token).HasMaxLength(100).IsRequired();
        builder.HasIndex(t => t.Token).IsUnique();

        builder.Property(t => t.Permissions)
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>());
    }
}
