using Agendamento.Domain.Common;
using Agendamento.Api.Domain.Quotations;
using System.Text;

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
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Service name cannot be empty.", nameof(name));
        
        if (string.IsNullOrWhiteSpace(unit))
            throw new ArgumentException("Service unit cannot be empty.", nameof(unit));

        ValidateLengths(name, description, unit);
        QuotationMoney.Validate(basePrice, nameof(basePrice));

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

        ValidateLengths(name, description, unit);
        QuotationMoney.Validate(basePrice, nameof(basePrice));

        Name = name;
        Description = description;
        Unit = unit;
        BasePrice = basePrice;
    }

    private static void ValidateLengths(string name, string? description, string unit)
    {
        if (ExceedsCharacters(name, 200))
            throw new ArgumentException("Service name cannot exceed 200 characters.", nameof(name));
        if (description is not null && ExceedsCharacters(description, 1000))
            throw new ArgumentException("Service description cannot exceed 1000 characters.", nameof(description));
        if (ExceedsCharacters(unit, 50))
            throw new ArgumentException("Service unit cannot exceed 50 characters.", nameof(unit));
    }

    // PostgreSQL varchar counts Unicode scalar characters, not UTF-16 code units.
    private static bool ExceedsCharacters(string value, int maximum)
    {
        var count = 0;
        foreach (var character in value.EnumerateRunes())
            if (++count > maximum) return true;
        return false;
    }

    public void Inactivate()
    {
        IsActive = false;
    }
}
