namespace Agendamento.Application.Identity;

/// <summary>Records an email-verification delivery request without talking to an external provider.</summary>
public interface IEmailVerificationSender
{
    Task RequestAsync(
        Guid userId,
        string normalizedEmail,
        Guid verificationTokenId,
        DateTimeOffset requestedAtUtc,
        CancellationToken cancellationToken = default);
}
