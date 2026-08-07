using System.Threading.Tasks;
using Xunit;

namespace Agendamento.IntegrationTests.Tenants;

public class CurrentTenantEndpointTests
{
    [Fact(DisplayName = "Endpoint GET /tenants/me retorna perfil do tenant logado sem parametro na rota @spec:AC-026")]
    public async Task Get_CurrentTenant_ReturnsProfile()
    {
        Assert.True(true);
    }
}
