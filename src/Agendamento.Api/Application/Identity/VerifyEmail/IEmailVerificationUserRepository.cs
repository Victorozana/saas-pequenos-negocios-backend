namespace Agendamento.Application.Identity.VerifyEmail;

public interface IEmailVerificationUserRepository
{
    Task<IEmailVerificationUser?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task SaveAsync(IEmailVerificationUser user, CancellationToken cancellationToken);
}
