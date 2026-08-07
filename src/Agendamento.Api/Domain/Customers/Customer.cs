using Agendamento.Domain.Common;

namespace Agendamento.Api.Domain.Customers;

public class Customer : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; set; }
    
    public string Name { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    
    public CustomerDocument? Document { get; private set; }
    public CustomerAddress? Address { get; private set; }
    
    public bool IsActive { get; private set; } = true;

    private Customer() { } // EF Core

    public static Customer Create(Guid tenantId, string name, string phone, string? email, CustomerDocument? document, CustomerAddress? address)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Customer name cannot be empty.", nameof(name));
            
        return new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Phone = phone,
            Email = email,
            Document = document,
            Address = address,
            IsActive = true
        };
    }

    public void Update(string name, string phone, string? email, CustomerDocument? document, CustomerAddress? address)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Customer name cannot be empty.", nameof(name));

        Name = name;
        Phone = phone;
        Email = email;
        Document = document;
        Address = address;
    }

    public void Inactivate()
    {
        IsActive = false;
    }
}
