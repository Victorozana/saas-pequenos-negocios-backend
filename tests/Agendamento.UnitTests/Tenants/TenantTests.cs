using Agendamento.Domain.Tenants;
using Xunit;

namespace Agendamento.UnitTests.Tenants;

public sealed class TenantTests
{
    [Fact(DisplayName = "Dados obrigatórios são validados na criação do Tenant @spec:AC-018")]
    public void Create_ThrowsArgumentException_WhenRequiredDataIsMissing()
    {
        var cnpj = Cnpj.Create("19131243000197");
        var address = TenantAddress.Create("Rua A", "123", "", "Centro", "São Paulo", "SP", "01000000");

        Assert.Throws<ArgumentException>(() => Tenant.Create(
            cnpj,
            "", // Missing CompanyName
            "Fantasia",
            "LTDA",
            "12345",
            BusinessCategory.Marmoraria,
            "contato@empresa.com",
            "11999999999",
            address));
            
        Assert.Throws<ArgumentNullException>(() => Tenant.Create(
            cnpj,
            "Empresa LTDA",
            "Fantasia",
            "LTDA",
            "12345",
            BusinessCategory.Marmoraria,
            "contato@empresa.com",
            "11999999999",
            null!)); // Missing Address
    }

    [Fact(DisplayName = "Cadastro inicial não coleta dados financeiros ou documentos @spec:AC-020")]
    public void Tenant_DoesNotContain_FinancialOrKycFields()
    {
        var tenantType = typeof(Tenant);
        
        // Ensure properties like BankAccount, Rg, Cnh, SocialContract do not exist on Tenant.
        Assert.Null(tenantType.GetProperty("BankAccount"));
        Assert.Null(tenantType.GetProperty("BankAgency"));
        Assert.Null(tenantType.GetProperty("Rg"));
        Assert.Null(tenantType.GetProperty("Cnh"));
        Assert.Null(tenantType.GetProperty("SocialContract"));
    }

    [Fact(DisplayName = "Tenant é criado com sucesso quando todos os dados são válidos")]
    public void Create_ReturnsTenant_WhenAllDataIsValid()
    {
        var cnpj = Cnpj.Create("19131243000197");
        var address = TenantAddress.Create("Rua A", "123", "", "Centro", "São Paulo", "SP", "01000000");

        var tenant = Tenant.Create(
            cnpj,
            "Empresa Teste LTDA",
            "Teste Fantasia",
            "Limitada",
            "5620101",
            BusinessCategory.Marmoraria,
            "contato@empresa.com",
            "11999999999",
            address
        );

        Assert.NotNull(tenant);
        Assert.NotEqual(Guid.Empty, tenant.Id);
        Assert.Equal("Empresa Teste LTDA", tenant.CompanyName);
        Assert.Equal("Pending", tenant.OnboardingStatus);
    }
}
