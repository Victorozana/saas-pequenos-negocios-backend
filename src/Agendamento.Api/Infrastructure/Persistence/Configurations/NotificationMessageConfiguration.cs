using Agendamento.Api.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agendamento.Api.Infrastructure.Persistence.Configurations;

public class NotificationMessageConfiguration : IEntityTypeConfiguration<NotificationMessage>
{
    public void Configure(EntityTypeBuilder<NotificationMessage> builder)
    {
        builder.ToTable("NotificationMessages", table => table.HasCheckConstraint("CK_NotificationMessages_AudienceCustomer",
            "(\"Audience\" = 'Customer' AND \"CustomerId\" IS NOT NULL) OR (\"Audience\" IN ('Standalone', 'System', 'LegacyUnlinked') AND \"CustomerId\" IS NULL)"));
        builder.Property(n => n.Audience).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.HasOne<Agendamento.Api.Domain.Customers.Customer>().WithMany()
            .HasForeignKey(n => new { n.TenantId, n.CustomerId })
            .HasPrincipalKey(c => new { c.TenantId, c.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(n => new { n.TenantId, n.CustomerId, n.CreatedAt });
        builder.HasIndex(n => new { n.TenantId, n.ReceivableTitleId, n.ReminderDate })
            .IsUnique().HasDatabaseName("UX_NotificationMessages_DailyReceivableReminder");
        builder.ToTable("NotificationMessages", table => table.HasCheckConstraint("CK_NotificationMessages_DailyReceivableReminder",
            "(\"ReceivableTitleId\" IS NULL AND \"ReminderDate\" IS NULL) OR (\"ReceivableTitleId\" IS NOT NULL AND \"ReminderDate\" IS NOT NULL AND \"Audience\" = 'Customer')"));
        builder.HasOne<Agendamento.Api.Domain.Financial.ReceivableTitle>().WithMany()
            .HasForeignKey(n => new { n.TenantId, n.ReceivableTitleId })
            .HasPrincipalKey(r => new { r.TenantId, r.Id }).OnDelete(DeleteBehavior.Restrict);

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

        builder.Property(n => n.NextRetryAt);

        // Index to optimize the Outbox worker query
        builder.HasIndex(n => new { n.TenantId, n.Status });
        builder.HasIndex(n => new { n.Status, n.NextRetryAt, n.CreatedAt });
    }
}
