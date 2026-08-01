namespace Agendamento.Application.Identity.VerifyEmail;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
