using System;

namespace Agendamento.Domain.Tenants;

public class TenantMembership
{
    private TenantMembership() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string Role { get; private set; } = null!;
    public bool IsLegalRepresentative { get; private set; }

    public static TenantMembership Create(Guid tenantId, Guid userId, string role, bool isLegalRepresentative)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        
        return new TenantMembership
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            Role = role,
            IsLegalRepresentative = isLegalRepresentative
        };
    }
}
