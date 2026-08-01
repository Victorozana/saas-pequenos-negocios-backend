using Agendamento.Domain.Identity;

namespace Agendamento.Application.Identity.VerifyEmail;

public interface IEmailVerificationTokenRepository
{
    Task<EmailVerificationToken?> FindByDigestAsync(string digest, CancellationToken cancellationToken);

    Task SaveAsync(EmailVerificationToken token, CancellationToken cancellationToken);
}
