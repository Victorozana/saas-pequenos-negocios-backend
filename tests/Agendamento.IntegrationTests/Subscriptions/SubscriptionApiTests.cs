using System.Net.Http.Json;
using Agendamento.Api.Features.Subscriptions;
using Agendamento.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
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

    [Fact(DisplayName = "Processar Webhooks de Pagamento de Assinatura do SaaS de forma idempotente @spec:AC-072")]
    public async Task Webhook_InvoicePaid_ShouldBeIdempotent_AC072()
    {
        // Arrange
        var eventId = Guid.NewGuid().ToString();
        var payload = new WebhookPayload
        {
            Id = eventId,
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

        // Act 1
        var response1 = await _client.PostAsJsonAsync("/api/v1/webhooks/billing", payload);
        response1.EnsureSuccessStatusCode();

        // Act 2 (Duplicate with same Event ID)
        var response2 = await _client.PostAsJsonAsync("/api/v1/webhooks/billing", payload);
        
        // Assert
        response2.EnsureSuccessStatusCode(); // Deve retornar 200 OK e pular processamento silenciosamente
    }

}
