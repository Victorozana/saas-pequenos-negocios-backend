namespace Agendamento.Domain.Tenants;

public sealed class Tenant
{
    private Tenant()
    {
    }

    public Guid Id { get; private set; }
    public Cnpj Cnpj { get; private set; } = null!;
    public string CompanyName { get; private set; } = null!;
    public string TradeName { get; private set; } = null!;
    public string LegalNature { get; private set; } = null!;
    public string PrimaryCnae { get; private set; } = null!;
    public BusinessCategory Category { get; private set; }
    public string Email { get; private set; } = null!;
    public string Phone { get; private set; } = null!;
    public TenantAddress Address { get; private set; } = null!;
    public string OnboardingStatus { get; private set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Tenant Create(
        Cnpj cnpj,
        string companyName,
        string tradeName,
        string legalNature,
        string primaryCnae,
        BusinessCategory category,
        string email,
        string phone,
        TenantAddress address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(companyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryCnae);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);
        ArgumentNullException.ThrowIfNull(cnpj);
        ArgumentNullException.ThrowIfNull(address);

        return new Tenant
        {
            Id = Guid.NewGuid(),
            Cnpj = cnpj,
            CompanyName = companyName.Trim(),
            TradeName = tradeName?.Trim() ?? string.Empty,
            LegalNature = legalNature?.Trim() ?? string.Empty,
            PrimaryCnae = primaryCnae.Trim(),
            Category = category,
            Email = email.Trim().ToLowerInvariant(),
            Phone = phone.Trim(),
            Address = address,
            OnboardingStatus = "Pending",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }
}
