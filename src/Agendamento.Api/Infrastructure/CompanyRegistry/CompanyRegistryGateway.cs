using System;
using System.Threading;
using System.Threading.Tasks;
using Agendamento.Api.Application.Tenants.CompanyRegistry;

namespace Agendamento.Api.Infrastructure.CompanyRegistry;

public class CompanyRegistryGateway : ICompanyRegistryGateway
{
    public Task<CompanyRegistryResult?> GetCompanyAsync(string cnpj, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Provedor não definido (Q-003). Use mocks nos testes.");
    }
}
