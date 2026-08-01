namespace Agendamento.Application.Identity.CreateSession;

/// <summary>Minimal authenticated context. Personal identifiers are intentionally absent.</summary>
public sealed record SessionPrincipal(Guid UserId, Guid TenantId, string Role);
