using System.Net;
using System.Text;
using Agendamento.Api.Application.Tenants.LookupCompany;
using Agendamento.Api.Infrastructure.CompanyRegistry;
using Xunit;

namespace Agendamento.IntegrationTests.Tenants;

public sealed class CompanyRegistryGatewayTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }

    [Fact(DisplayName = "Gateway consulta URL exata e mapeia todos os dados públicos @spec:AC-017")]
    public async Task Gateway_ShouldUseExactUrlAndMapCompletePayload()
    {
        Uri? requestedUri = null;
        const string json = """
            {
              "cnpj": "00000000000191",
              "razao_social": "Empresa Exemplo LTDA",
              "nome_fantasia": "Empresa Exemplo",
              "natureza_juridica": "Sociedade Empresária Limitada",
              "cnae_fiscal": 2391503,
              "cnaes_secundarios": [{ "codigo": 4744005 }, { "codigo": "4330404" }],
              "descricao_situacao_cadastral": "ATIVA",
              "logradouro": "Rua das Flores",
              "numero": "10",
              "complemento": "Sala 2",
              "bairro": "Centro",
              "municipio": "São Paulo",
              "uf": "SP",
              "cep": "01001000"
            }
            """;
        var client = new HttpClient(new StubHandler((request, _) =>
        {
            requestedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        })) { BaseAddress = new Uri("https://brasilapi.com.br/") };
        var gateway = new CompanyRegistryGateway(client);

        var result = await gateway.GetCompanyAsync("00000000000191");

        Assert.Equal("https://brasilapi.com.br/api/cnpj/v1/00000000000191", requestedUri?.AbsoluteUri);
        Assert.NotNull(result);
        Assert.Equal("00000000000191", result.Cnpj);
        Assert.Equal("Empresa Exemplo LTDA", result.CorporateName);
        Assert.Equal("Empresa Exemplo", result.TradeName);
        Assert.Equal("Sociedade Empresária Limitada", result.LegalNature);
        Assert.Equal(new[] { "2391503", "4744005", "4330404" }, result.Cnaes);
        Assert.Equal("ATIVA", result.Status);
        Assert.Equal("Rua das Flores", result.Address.Street);
        Assert.Equal("10", result.Address.Number);
        Assert.Equal("Sala 2", result.Address.Complement);
        Assert.Equal("Centro", result.Address.Neighborhood);
        Assert.Equal("São Paulo", result.Address.City);
        Assert.Equal("SP", result.Address.State);
        Assert.Equal("01001000", result.Address.ZipCode);
    }

    [Fact(DisplayName = "Gateway traduz 404 para empresa ausente @spec:AC-017")]
    public async Task Gateway_ShouldReturnNullForNotFound()
    {
        var gateway = CreateGateway((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

        var result = await gateway.GetCompanyAsync("00000000000191");

        Assert.Null(result);
    }

    [Theory(DisplayName = "Gateway converte status externo em indisponibilidade segura @spec:AC-017")]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Gateway_ShouldRejectExternalFailure(HttpStatusCode status)
    {
        var gateway = CreateGateway((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent("provider detail leaked"),
        }));

        var ex = await Assert.ThrowsAsync<CompanyRegistryUnavailableException>(() =>
            gateway.GetCompanyAsync("00000000000191"));

        Assert.Equal("Não foi possível consultar o CNPJ agora.", ex.Message);
    }

    [Fact(DisplayName = "Gateway rejeita payload externo inválido com falha segura @spec:AC-017")]
    public async Task Gateway_ShouldRejectMalformedPayload()
    {
        var gateway = CreateGateway((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json", Encoding.UTF8, "application/json"),
        }));

        await Assert.ThrowsAsync<CompanyRegistryUnavailableException>(() =>
            gateway.GetCompanyAsync("00000000000191"));
    }

    [Fact(DisplayName = "Gateway converte timeout em indisponibilidade segura @spec:AC-017")]
    public async Task Gateway_ShouldTranslateTimeout()
    {
        var gateway = CreateGateway((_, _) => throw new TaskCanceledException("provider timeout"));

        await Assert.ThrowsAsync<CompanyRegistryUnavailableException>(() =>
            gateway.GetCompanyAsync("00000000000191"));
    }

    private static CompanyRegistryGateway CreateGateway(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
    {
        var client = new HttpClient(new StubHandler(send)) { BaseAddress = new Uri("https://brasilapi.com.br/") };
        return new CompanyRegistryGateway(client);
    }
}
