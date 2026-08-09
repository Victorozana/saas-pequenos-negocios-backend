using Agendamento.Domain.Common;

namespace Agendamento.Api.Domain.Notifications;

public class NotificationMessage : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; set; }
    
    public string Recipient { get; private set; } = string.Empty;
    public NotificationChannel Channel { get; private set; }
    
    public string Content { get; private set; } = string.Empty;
    public NotificationStatus Status { get; private set; }
    
    public int RetryCount { get; private set; }
    public string? LastError { get; private set; }
    
    public DateTime CreatedAt { get; private set; }
    public DateTime? SentAt { get; private set; }

    private NotificationMessage() { } // EF Core

    public static NotificationMessage Create(Guid tenantId, string recipient, NotificationChannel channel, string content)
    {
        if (string.IsNullOrWhiteSpace(recipient))
            throw new ArgumentException("Recipient cannot be empty.", nameof(recipient));
        
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty.", nameof(content));

        return new NotificationMessage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
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
        }
    }

    public void Cancel()
    {
        Status = NotificationStatus.Cancelled;
    }
}
