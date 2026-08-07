using System;
using System.Threading;
using System.Threading.Tasks;
using Agendamento.Api.Application.Tenants.CompanyRegistry;
using Agendamento.Domain.Tenants;

namespace Agendamento.Api.Application.Tenants.LookupCompany;

public class LookupCompanyHandler
{
    private readonly ICompanyRegistryGateway _gateway;

    public LookupCompanyHandler(ICompanyRegistryGateway gateway)
    {
        _gateway = gateway;
    }

    public async Task<CompanyRegistryResult> HandleAsync(LookupCompanyQuery query, CancellationToken cancellationToken = default)
    {
        Cnpj validCnpj;
        try
        {
            validCnpj = Cnpj.Create(query.Cnpj);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException("CNPJ inválido.", ex);
        }

        var result = await _gateway.GetCompanyAsync(validCnpj.Value, cancellationToken);
        if (result == null)
        {
            throw new InvalidOperationException("Empresa não encontrada.");
        }

        if (!string.Equals(result.Status, "ATIVA", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("A empresa não está ativa.");
        }

        if (!FoodCnaePolicy.IsEligible(result.Cnaes))
        {
            throw new InvalidOperationException("A atividade não é elegível.");
        }

        return result;
    }
}
