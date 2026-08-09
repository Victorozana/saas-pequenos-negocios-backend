using Agendamento.Api.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendamento.Api.Infrastructure.Persistence.Configurations;

public class NotificationMessageConfiguration : IEntityTypeConfiguration<NotificationMessage>
{
    public void Configure(EntityTypeBuilder<NotificationMessage> builder)
    {
        builder.ToTable("NotificationMessages");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.TenantId)
            .IsRequired();

        builder.Property(n => n.Recipient)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(n => n.Channel)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(n => n.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(n => n.Content)
            .IsRequired();

        builder.Property(n => n.LastError)
            .HasMaxLength(2000);

        builder.Property(n => n.RetryCount)
            .IsRequired();

        builder.Property(n => n.CreatedAt)
            .IsRequired();

        // Index to optimize the Outbox worker query
        builder.HasIndex(n => new { n.TenantId, n.Status });
    }
}
