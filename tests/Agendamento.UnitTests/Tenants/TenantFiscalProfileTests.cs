using Agendamento.Domain.Tenants;
using Xunit;

namespace Agendamento.UnitTests.Tenants;

public sealed class TenantFiscalProfileTests
{
    [Fact(DisplayName = "Perfil fiscal pertence exclusivamente ao tenant @spec:AC-019")]
    public void Create_AssociatesExclusivelyToTenant_WithValidFiscalData()
    {
        var tenantId = Guid.NewGuid();
        var fiscalProfile = TenantFiscalProfile.Create(
            tenantId,
            "123456789",
            "987654321",
            isTaxExempt: false,
            TaxRegime.SimplesNacional,
            "fiscal@empresa.com"
        );

        Assert.Equal(tenantId, fiscalProfile.TenantId);
        Assert.Equal("123456789", fiscalProfile.StateRegistration);
        Assert.Equal("987654321", fiscalProfile.MunicipalRegistration);
        Assert.False(fiscalProfile.IsTaxExempt);
        Assert.Equal(TaxRegime.SimplesNacional, fiscalProfile.TaxRegime);
        Assert.Equal("fiscal@empresa.com", fiscalProfile.FiscalEmail);
    }
}
