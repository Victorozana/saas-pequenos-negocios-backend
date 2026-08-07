using System.Threading;
using System.Threading.Tasks;

namespace Agendamento.Api.Application.Tenants.CompanyRegistry;

public interface ICompanyRegistryGateway
{
    Task<CompanyRegistryResult?> GetCompanyAsync(string cnpj, CancellationToken cancellationToken = default);
}
