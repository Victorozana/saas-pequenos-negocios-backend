using Agendamento.Domain.Common;

namespace Agendamento.Api.Domain.Services;

public class ServiceItem : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; set; }
    
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string Unit { get; private set; } = string.Empty;
    public decimal BasePrice { get; private set; }
    
    public bool IsActive { get; private set; } = true;

    private ServiceItem() { } // EF Core

    public static ServiceItem Create(Guid tenantId, string name, string? description, string unit, decimal basePrice)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Service name cannot be empty.", nameof(name));
        
        if (string.IsNullOrWhiteSpace(unit))
            throw new ArgumentException("Service unit cannot be empty.", nameof(unit));

        if (basePrice < 0)
            throw new ArgumentException("Base price cannot be negative.", nameof(basePrice));

        return new ServiceItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Description = description,
            Unit = unit,
            BasePrice = basePrice,
            IsActive = true
        };
    }

    public void Update(string name, string? description, string unit, decimal basePrice)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Service name cannot be empty.", nameof(name));

        if (string.IsNullOrWhiteSpace(unit))
            throw new ArgumentException("Service unit cannot be empty.", nameof(unit));

        if (basePrice < 0)
            throw new ArgumentException("Base price cannot be negative.", nameof(basePrice));

        Name = name;
        Description = description;
        Unit = unit;
        BasePrice = basePrice;
    }

    public void Inactivate()
    {
        IsActive = false;
    }
}
