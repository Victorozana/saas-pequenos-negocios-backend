using System.Net.Http.Json;
using System.Text.Json;
using Agendamento.IntegrationTests.Features;
using Agendamento.Api.Domain.Quotations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Agendamento.IntegrationTests.Quotations;

public sealed class QuotationConversionContractTests
{
    [Fact(DisplayName = "Hosts de prova conservam bancos InMemory isolados mesmo com o mesmo tenant @spec:AC-092")]
    public async Task IndependentHostsDoNotShareTenantData()
    {
        using var first = new TeamFinancialTestFactory();
        using var second = new TeamFinancialTestFactory();
        var tenant = Guid.NewGuid();
        var (firstClient, _, _) = await first.Member("owner_admin", [], tenant);
        var (secondClient, _, _) = await second.Member("owner_admin", [], tenant);
        var customer = Agendamento.Api.Domain.Customers.Customer.Create(tenant, "Host isolation", "123", null, null, null);
        var quote = Quotation.Create(tenant, "host-isolation", customer.Id, DateTime.UtcNow, null, null, null);
        quote.AddItem(QuotationItem.Create(null, "service", "un", 10m, 1m, 0m));
        quote.MarkAsPending();
        using (var db = first.Db(tenant)) { db.Add(customer); db.Add(quote); await db.SaveChangesAsync(); }
        Assert.Equal(System.Net.HttpStatusCode.OK, (await firstClient.GetAsync($"/api/v1/quotations/{quote.Id}")).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, (await secondClient.GetAsync($"/api/v1/quotations/{quote.Id}")).StatusCode);
        using var otherDb = second.Db(tenant);
        Assert.Empty(await otherDb.Quotations.ToListAsync());
    }

    [Fact(DisplayName = "OpenAPI live preserva respostas distintas da conversão e documenta Location na rota alternativa @spec:AC-037 @spec:AC-091")]
    public async Task BothConversionContractsRemainDocumented()
    {
        using var factory = new TeamFinancialTestFactory();
        using var client = factory.CreateClient();
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        var paths = document.GetProperty("paths");
        var standard = paths.GetProperty("/api/v1/quotations/{id}/convert").GetProperty("post");
        var alternative = paths.GetProperty("/api/v1/work-orders/from-quotation/{quotationId}").GetProperty("post");
        Assert.Equal("#/components/schemas/WorkOrderConversionResponse", standard.GetProperty("responses").GetProperty("200")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString());
        Assert.Equal("#/components/schemas/CreatedIdResponse", alternative.GetProperty("responses").GetProperty("201")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString());
        Assert.Equal("string", alternative.GetProperty("responses").GetProperty("201").GetProperty("headers")
            .GetProperty("Location").GetProperty("schema").GetProperty("type").GetString());
        foreach (var operation in new[] { standard, alternative })
        {
            Assert.False(operation.TryGetProperty("requestBody", out _));
            Assert.True(operation.GetProperty("responses").GetProperty("400").GetProperty("content")
                .GetProperty("application/problem+json").TryGetProperty("schema", out _));
            Assert.True(operation.GetProperty("responses").TryGetProperty("401", out _));
            Assert.True(operation.GetProperty("responses").TryGetProperty("403", out _));
        }
    }
}
