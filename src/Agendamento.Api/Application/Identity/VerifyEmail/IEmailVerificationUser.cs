namespace Agendamento.Application.Identity.VerifyEmail;

/// <summary>Minimal account state needed by the verification use case.</summary>
public interface IEmailVerificationUser
{
    Guid Id { get; }

    void ConfirmEmail(DateTimeOffset confirmedAtUtc);
}
