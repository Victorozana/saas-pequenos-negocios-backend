namespace Agendamento.Application.Identity.CreateSession;

/// <summary>Credentials supplied when an already verified person starts a session.</summary>
public sealed record CreateSessionCommand(string Email, string Password);
