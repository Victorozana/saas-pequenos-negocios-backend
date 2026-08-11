using System;
using System.Collections.Generic;
using Agendamento.Domain.Common;

namespace Agendamento.Domain.Tenants;

public enum TeamInvitationStatus
{
    Pending,
    Accepted,
    Expired
}

public class TeamInvitation : ITenantOwned
{
    private TeamInvitation() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; set; }
    public string Email { get; private set; } = null!;
    public string Role { get; private set; } = null!;
    public List<string> Permissions { get; private set; } = new();
    public string Token { get; private set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public TeamInvitationStatus Status { get; private set; }

    public static TeamInvitation Create(Guid tenantId, string email, string role, List<string> permissions, int expirationDays = 7)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        var now = DateTimeOffset.UtcNow;
        return new TeamInvitation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email.Trim().ToLowerInvariant(),
            Role = role,
            Permissions = permissions ?? new List<string>(),
            Token = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(expirationDays),
            Status = TeamInvitationStatus.Pending
        };
    }

    public void Accept()
    {
        if (Status != TeamInvitationStatus.Pending)
            throw new InvalidOperationException("O convite não está pendente.");

        if (DateTimeOffset.UtcNow > ExpiresAtUtc)
        {
            Status = TeamInvitationStatus.Expired;
            throw new InvalidOperationException("O convite está expirado.");
        }

        Status = TeamInvitationStatus.Accepted;
    }
}
