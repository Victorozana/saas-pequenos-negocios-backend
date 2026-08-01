using Agendamento.Domain.Identity;

namespace Agendamento.Application.Identity.VerifyEmail;

public sealed class VerifyEmailHandler(
    IEmailVerificationTokenRepository tokens,
    IEmailVerificationUserRepository users,
    IClock clock)
{
    public async Task<VerifyEmailResult> HandleAsync(
        VerifyEmailCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Token))
        {
            return VerifyEmailResult.InvalidToken;
        }

        var token = await tokens.FindByDigestAsync(
            EmailVerificationToken.ComputeDigest(command.Token), cancellationToken);

        if (token is null || !token.Matches(command.Token) || !token.TryConsume(clock.UtcNow))
        {
            return VerifyEmailResult.InvalidToken;
        }

        var user = await users.FindByIdAsync(token.UserId, cancellationToken);
        if (user is null)
        {
            return VerifyEmailResult.InvalidToken;
        }

        user.ConfirmEmail(clock.UtcNow);
        await tokens.SaveAsync(token, cancellationToken);
        await users.SaveAsync(user, cancellationToken);
        return VerifyEmailResult.Verified;
    }
}
