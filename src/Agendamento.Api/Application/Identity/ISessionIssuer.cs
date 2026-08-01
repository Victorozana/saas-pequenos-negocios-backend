using Agendamento.Application.Identity.CreateSession;

namespace Agendamento.Application.Identity;

public interface ISessionIssuer
{
    IssuedSession Issue(SessionPrincipal principal);
}

public sealed record IssuedSession(string AccessToken, DateTimeOffset ExpiresAt);
