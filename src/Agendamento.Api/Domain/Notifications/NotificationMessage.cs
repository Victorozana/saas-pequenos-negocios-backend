using Agendamento.Domain.Common;

namespace Agendamento.Api.Domain.Notifications;

public class NotificationMessage : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; set; }
    public Guid? CustomerId { get; private set; }
    public NotificationAudience Audience { get; private set; }
    public Guid? ReceivableTitleId { get; private set; }
    public DateOnly? ReminderDate { get; private set; }

    internal void AssociateDailyReceivableReminder(Guid titleId, DateOnly date)
    {
        if (titleId == Guid.Empty || Audience != NotificationAudience.Customer || ReceivableTitleId.HasValue)
            throw new InvalidOperationException("Daily reminders require a customer and a valid title.");
        ReceivableTitleId = titleId;
        ReminderDate = date;
    }
    
    public string Recipient { get; private set; } = string.Empty;
    public NotificationChannel Channel { get; private set; }
    
    public string Content { get; private set; } = string.Empty;
    public NotificationStatus Status { get; private set; }
    
    public int RetryCount { get; private set; }
    public string? LastError { get; private set; }
    
    public DateTime CreatedAt { get; private set; }
    public DateTime? SentAt { get; private set; }
    public DateTime? NextRetryAt { get; private set; }

    private NotificationMessage() { } // EF Core

    public static NotificationMessage Create(Guid tenantId, string recipient, NotificationChannel channel, string content, Guid? customerId = null, NotificationAudience audience = NotificationAudience.Standalone)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant is required.", nameof(tenantId));
        if (!Enum.IsDefined(channel)) throw new ArgumentException("Invalid channel.", nameof(channel));
        if (!Enum.IsDefined(audience) || audience == NotificationAudience.LegacyUnlinked)
            throw new ArgumentException("Legacy classification is reserved for migration.", nameof(audience));
        if ((audience == NotificationAudience.Customer) != customerId.HasValue || customerId == Guid.Empty)
            throw new ArgumentException("Customer notifications require a customer; other audiences cannot have one.", nameof(customerId));
        if (string.IsNullOrWhiteSpace(recipient) || recipient.Length > 255)
            throw new ArgumentException("Recipient cannot be empty.", nameof(recipient));
        
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty.", nameof(content));

        return new NotificationMessage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerId = customerId,
            Audience = audience,
            Recipient = recipient,
            Channel = channel,
            Content = content,
            Status = NotificationStatus.Pending,
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkAsSent()
    {
        Status = NotificationStatus.Sent;
        SentAt = DateTime.UtcNow;
        LastError = null;
    }

    public void MarkAsFailed(string error)
    {
        RetryCount++;
        LastError = error;
        
        if (RetryCount >= 3)
        {
            Status = NotificationStatus.Failed;
            NextRetryAt = null;
        }
        else
        {
            // Exponential backoff: 2^RetryCount minutes (e.g. 2m, 4m)
            NextRetryAt = DateTime.UtcNow.AddMinutes(Math.Pow(2, RetryCount));
        }
    }

    public void Cancel()
    {
        Status = NotificationStatus.Cancelled;
    }
}
