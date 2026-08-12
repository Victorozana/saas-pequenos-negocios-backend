using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Agendamento.Domain.Identity;

namespace Agendamento.Api.Infrastructure.Persistence.Configurations;

public class UserEfConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.Name).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Cpf).HasMaxLength(11);
        builder.Property(u => u.Email).HasMaxLength(320).IsRequired();
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.Status).IsRequired();
        builder.Property(u => u.CreatedAtUtc).IsRequired();
        builder.Property(u => u.EmailVerifiedAtUtc);

        builder.HasIndex(u => u.Cpf).IsUnique();
        builder.HasIndex(u => u.Email).IsUnique();
    }
}
