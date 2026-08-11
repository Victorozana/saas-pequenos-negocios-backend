using System;

using Agendamento.Domain.Common;

namespace Agendamento.Domain.Tenants;

public class TenantMembership : ITenantOwned
{
    private TenantMembership() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; private set; }
    public string Role { get; private set; } = null!;
    public bool IsLegalRepresentative { get; private set; }
    public List<string> Permissions { get; private set; } = new();

    public static TenantMembership Create(Guid tenantId, Guid userId, string role, bool isLegalRepresentative, List<string>? permissions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        
        var membership = new TenantMembership
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            Role = role,
            IsLegalRepresentative = isLegalRepresentative
        };

        if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            membership.Permissions = Agendamento.Domain.Identity.Permissions.All.ToList();
        }
        else if (permissions != null)
        {
            membership.Permissions = permissions;
        }

        return membership;
    }

    public void UpdateRoleAndPermissions(string role, List<string> permissions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        Role = role;
        
        if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            Permissions = Agendamento.Domain.Identity.Permissions.All.ToList();
        }
        else
        {
            Permissions = permissions ?? new List<string>();
        }
    }

    public bool HasPermission(string permission)
    {
        if (Role.Equals("Admin", StringComparison.OrdinalIgnoreCase)) return true;
        return Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    }
}
