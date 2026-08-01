using System.Security.Cryptography;
using System.Text;

namespace Agendamento.Domain.Identity;

/// <summary>
/// A one-time email-verification credential. Only the SHA-256 digest is persisted.
/// </summary>
public sealed class EmailVerificationToken
{
    private EmailVerificationToken(
        Guid id,
        Guid userId,
        string digest,
        DateTimeOffset expiresAtUtc,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        UserId = userId;
        Digest = digest;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; }

    public Guid UserId { get; }

    /// <summary>SHA-256 digest encoded as lowercase hexadecimal; never the raw token.</summary>
    public string Digest { get; }

    public DateTimeOffset ExpiresAtUtc { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset? ConsumedAtUtc { get; private set; }

    public static EmailVerificationToken Create(Guid userId, string rawToken, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);

        return new EmailVerificationToken(
            Guid.NewGuid(),
            userId,
            ComputeDigest(rawToken),
            nowUtc.AddHours(24),
            nowUtc);
    }

    public bool Matches(string rawToken) =>
        !string.IsNullOrWhiteSpace(rawToken) &&
        CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(Digest),
            SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    public bool CanBeConsumedAt(DateTimeOffset nowUtc) =>
        ConsumedAtUtc is null && nowUtc <= ExpiresAtUtc;

    public bool TryConsume(DateTimeOffset nowUtc)
    {
        if (!CanBeConsumedAt(nowUtc))
        {
            return false;
        }

        ConsumedAtUtc = nowUtc;
        return true;
    }

    public static string ComputeDigest(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();
    }
}
