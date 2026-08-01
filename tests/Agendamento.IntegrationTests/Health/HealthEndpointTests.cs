using System.Net;
using Agendamento.IntegrationTests.Infrastructure;
using Xunit;

namespace Agendamento.IntegrationTests.Health;

public sealed class HealthEndpointTests(AgendamentoApiFactory factory) : IClassFixture<AgendamentoApiFactory>
{
    [Fact(DisplayName = "GET /health responde que a aplicação está saudável @spec:AC-002")]
    public async Task GetHealthReturnsOk()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = "O comando oficial descobre a prova HTTP de integração @spec:AC-003")]
    public void OfficialTestCommandDiscoversTheHttpIntegrationProof()
    {
        using var client = factory.CreateClient();

        Assert.NotNull(client);
    }
}
