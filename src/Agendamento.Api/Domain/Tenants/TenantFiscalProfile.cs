using Agendamento.Domain.Common;

namespace Agendamento.Domain.Tenants;

public sealed class TenantFiscalProfile : ITenantOwned
{
    private TenantFiscalProfile()
    {
    }

    public Guid TenantId { get; set; }
    public string StateRegistration { get; private set; } = null!;
    public string MunicipalRegistration { get; private set; } = null!;
    public bool IsTaxExempt { get; private set; }
    public TaxRegime TaxRegime { get; private set; }
    public string FiscalEmail { get; private set; } = null!;

    public static TenantFiscalProfile Create(
        Guid tenantId,
        string stateRegistration,
        string municipalRegistration,
        bool isTaxExempt,
        TaxRegime taxRegime,
        string fiscalEmail)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId inválido.", nameof(tenantId));
            
        ArgumentException.ThrowIfNullOrWhiteSpace(fiscalEmail);

        return new TenantFiscalProfile
        {
            TenantId = tenantId,
            StateRegistration = stateRegistration ?? string.Empty,
            MunicipalRegistration = municipalRegistration ?? string.Empty,
            IsTaxExempt = isTaxExempt,
            TaxRegime = taxRegime,
            FiscalEmail = fiscalEmail.Trim().ToLowerInvariant()
        };
    }
}
