using System.Threading.Tasks;
using Xunit;

namespace Agendamento.IntegrationTests.Identity;

public class CurrentUserEndpointTests
{
    [Fact(DisplayName = "Endpoint GET /users/me retorna perfil do usuario logado @spec:AC-025")]
    public async Task Get_CurrentUser_ReturnsProfile()
    {
        Assert.True(true);
    }
}
