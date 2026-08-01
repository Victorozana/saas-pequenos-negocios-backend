namespace Agendamento.Application.Identity.CreateSession;

public sealed record CreateSessionResult(string AccessToken, DateTimeOffset ExpiresAt);
