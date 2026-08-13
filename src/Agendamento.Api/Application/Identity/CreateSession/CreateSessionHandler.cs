using Agendamento.Application.Identity;
using Agendamento.Domain.Identity;

namespace Agendamento.Application.Identity.CreateSession;

public sealed class CreateSessionHandler(
    IIdentityAuthenticationStore identities,
    IPasswordService passwords,
    ISessionIssuer sessions)
{
    public async Task<CreateSessionOutcome> HandleAsync(
        CreateSessionCommand command,
        CancellationToken cancellationToken = default)
    {
        string normalizedEmail;
        try
        {
            normalizedEmail = User.NormalizeEmail(command.Email);
        }
        catch (ArgumentException)
        {
            return CreateSessionOutcome.InvalidCredentials;
        }
        var identity = await identities.FindByNormalizedEmailAsync(normalizedEmail, cancellationToken);

        if (identity is null || !passwords.Verify(identity.PasswordHash, command.Password))
        {
            return CreateSessionOutcome.InvalidCredentials;
        }

        if (!identity.IsEmailVerified)
        {
            return CreateSessionOutcome.EmailVerificationRequired;
        }

        if (!identity.IsActive)
        {
            return CreateSessionOutcome.InvalidCredentials;
        }

        if (identity.ActiveMembership is null)
        {
            return CreateSessionOutcome.InvalidCredentials;
        }

        var issued = sessions.Issue(new SessionPrincipal(
            identity.UserId,
            identity.ActiveMembership.TenantId,
            identity.ActiveMembership.Role));

        return new CreateSessionOutcome(issued.AccessToken, issued.ExpiresAt, null);
    }
}

public sealed record CreateSessionOutcome(string? AccessToken, DateTimeOffset? ExpiresAt, string? Error)
{
    public static readonly CreateSessionOutcome InvalidCredentials = new(null, null, "invalid_credentials");
    public static readonly CreateSessionOutcome EmailVerificationRequired = new(null, null, "email_verification_required");
}
