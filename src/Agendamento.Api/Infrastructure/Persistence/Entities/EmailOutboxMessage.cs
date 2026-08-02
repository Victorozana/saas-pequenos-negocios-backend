namespace Agendamento.Infrastructure.Persistence.Entities;

/// <summary>
/// Durable request for verification email delivery. It deliberately contains no raw token.
/// </summary>
public sealed class EmailOutboxMessage
{
    private EmailOutboxMessage(
        Guid id,
        Guid userId,
        string normalizedEmail,
        Guid verificationTokenId,
        DateTimeOffset occurredAtUtc)
    {
        Id = id;
        UserId = userId;
        NormalizedEmail = normalizedEmail;
        VerificationTokenId = verificationTokenId;
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid Id { get; }

    public Guid UserId { get; }

    public string NormalizedEmail { get; }

    public Guid VerificationTokenId { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public DateTimeOffset? DispatchedAtUtc { get; private set; }

    public static EmailOutboxMessage Create(
        Guid userId,
        string normalizedEmail,
        Guid verificationTokenId,
        DateTimeOffset occurredAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);
        return new EmailOutboxMessage(Guid.NewGuid(), userId, normalizedEmail, verificationTokenId, occurredAtUtc);
    }

    public void MarkDispatched(DateTimeOffset dispatchedAtUtc) => DispatchedAtUtc ??= dispatchedAtUtc;
}
