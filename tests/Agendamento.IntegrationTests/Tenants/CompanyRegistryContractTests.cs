using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agendamento.Api.Application.Tenants.CompanyRegistry;
using Agendamento.Api.Application.Tenants.LookupCompany;
using Xunit;

namespace Agendamento.IntegrationTests.Tenants;

public class CompanyRegistryContractTests
{
    private class FakeGateway : ICompanyRegistryGateway
    {
        public Func<string, CompanyRegistryResult?>? GetCompanyAsyncMock { get; set; }

        public Task<CompanyRegistryResult?> GetCompanyAsync(string cnpj, CancellationToken cancellationToken = default)
        {
            if (GetCompanyAsyncMock != null)
            {
                return Task.FromResult(GetCompanyAsyncMock(cnpj));
            }
            return Task.FromResult<CompanyRegistryResult?>(null);
        }
    }

    [Fact(DisplayName = "Handler throws when CNPJ is invalid @spec:AC-014")]
    public async Task Handler_ShouldThrowInvalidOperationException_WhenCnpjIsInvalid()
    {
        // @spec: AC-014
        var gateway = new FakeGateway();
        var handler = new LookupCompanyHandler(gateway);
        var query = new LookupCompanyQuery("invalid-cnpj");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(query));
        Assert.Contains("CNPJ inválido", ex.Message);
    }

    [Fact(DisplayName = "Handler throws when company is inactive @spec:AC-015")]
    public async Task Handler_ShouldThrowInvalidOperationException_WhenCompanyIsInactive()
    {
        // @spec: AC-015
        var gateway = new FakeGateway
        {
            GetCompanyAsyncMock = (cnpj) => new CompanyRegistryResult(
                Cnpj: cnpj,
                CorporateName: "Inativa Corp",
                TradeName: "",
                LegalNature: "LTDA",
                Cnaes: new List<string> { "5611201" },
                Status: "BAIXADA",
                Address: new RegistryAddress("Rua", "1", "", "Bairro", "Cidade", "SP", "01000000")
            )
        };
        var handler = new LookupCompanyHandler(gateway);
        var query = new LookupCompanyQuery("00000000000191"); 

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(query));
        Assert.Contains("não está ativa", ex.Message);
    }

    [Fact(DisplayName = "Handler throws when company has no food CNAE @spec:AC-016")]
    public async Task Handler_ShouldThrowInvalidOperationException_WhenCompanyHasNoFoodCnae()
    {
        // @spec: AC-016
        var gateway = new FakeGateway
        {
            GetCompanyAsyncMock = (cnpj) => new CompanyRegistryResult(
                Cnpj: cnpj,
                CorporateName: "Tech Corp",
                TradeName: "",
                LegalNature: "LTDA",
                Cnaes: new List<string> { "6204000" }, // Not food
                Status: "ATIVA",
                Address: new RegistryAddress("Rua", "1", "", "Bairro", "Cidade", "SP", "01000000")
            )
        };
        var handler = new LookupCompanyHandler(gateway);
        var query = new LookupCompanyQuery("00000000000191");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(query));
        Assert.Contains("elegível", ex.Message);
    }

    [Fact(DisplayName = "Handler returns company data when eligible @spec:AC-017")]
    public async Task Handler_ShouldReturnCompanyData_WhenEligible()
    {
        // @spec: AC-017
        var gateway = new FakeGateway
        {
            GetCompanyAsyncMock = (cnpj) => new CompanyRegistryResult(
                Cnpj: cnpj,
                CorporateName: "Food Corp",
                TradeName: "Food",
                LegalNature: "LTDA",
                Cnaes: new List<string> { "5611201" }, // Food
                Status: "ATIVA",
                Address: new RegistryAddress("Rua", "1", "", "Bairro", "Cidade", "SP", "01000000")
            )
        };
        var handler = new LookupCompanyHandler(gateway);
        var query = new LookupCompanyQuery("00000000000191");

        var result = await handler.HandleAsync(query);

        Assert.NotNull(result);
        Assert.Equal("Food Corp", result.CorporateName);
        Assert.Equal("ATIVA", result.Status);
    }
}
