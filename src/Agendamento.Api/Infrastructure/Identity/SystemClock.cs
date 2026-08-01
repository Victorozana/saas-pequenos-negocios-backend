using Agendamento.Application.Identity.VerifyEmail;

namespace Agendamento.Infrastructure.Identity;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
