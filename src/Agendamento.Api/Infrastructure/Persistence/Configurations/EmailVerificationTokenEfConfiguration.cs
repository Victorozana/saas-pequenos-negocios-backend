using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Agendamento.Domain.Identity;

namespace Agendamento.Api.Infrastructure.Persistence.Configurations;

public class EmailVerificationTokenEfConfiguration : IEntityTypeConfiguration<EmailVerificationToken>
{
    public void Configure(EntityTypeBuilder<EmailVerificationToken> builder)
    {
        builder.ToTable("EmailVerificationTokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.UserId).IsRequired();
        builder.Property(t => t.Digest).HasMaxLength(64).IsRequired();
        builder.HasIndex(t => t.Digest).IsUnique();

        builder.Property(t => t.ExpiresAtUtc).IsRequired();
        builder.Property(t => t.CreatedAtUtc).IsRequired();
        builder.Property(t => t.ConsumedAtUtc);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
