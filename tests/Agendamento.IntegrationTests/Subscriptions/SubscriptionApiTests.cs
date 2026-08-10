using System.Net.Http.Json;
using Agendamento.Api.Features.Subscriptions;
using Agendamento.IntegrationTests.Infrastructure;
using Xunit;

namespace Agendamento.IntegrationTests.Subscriptions;

public class SubscriptionApiTests : IClassFixture<AgendamentoApiFactory>, IAsyncLifetime
{
    private readonly AgendamentoApiFactory _factory;
    private HttpClient _client = null!;

    public SubscriptionApiTests(AgendamentoApiFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Webhook_InvoicePaid_ShouldReturnOk()
    {
        // Arrange
        var payload = new WebhookPayload
        {
            Type = "invoice.paid",
            Data = new WebhookData
            {
                Object = new WebhookObject
                {
                    Id = "sub_unknown123",
                    CurrentPeriodStart = DateTimeOffset.UtcNow,
                    CurrentPeriodEnd = DateTimeOffset.UtcNow.AddMonths(1)
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/webhooks/billing", payload);

        // Assert
        response.EnsureSuccessStatusCode();
        // Nota: Idealmente testaríamos se a base foi atualizada, mas aqui validamos pelo menos o parsing do payload e a resposta idempotente (200 OK mesmo que sub_unknown123 não exista).
    }
}
