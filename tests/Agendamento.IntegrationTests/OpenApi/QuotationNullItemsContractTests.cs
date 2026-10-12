using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Agendamento.Api.Domain.Customers;
using Agendamento.Api.Domain.Quotations;
using Agendamento.IntegrationTests.Features;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Agendamento.IntegrationTests.OpenApi;

public sealed class QuotationNullItemsContractTests : IClassFixture<TeamFinancialTestFactory>
{
    private readonly TeamFinancialTestFactory factory;
    public QuotationNullItemsContractTests(TeamFinancialTestFactory factory) => this.factory = factory;

    [Theory(DisplayName = "Criação de orçamento rejeita coleção ou elemento null com ProblemDetails sem persistir @spec:AC-037")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateRejectsNullItemsWithoutPersisting(bool nullElement)
    {
        var (client, _, tenant) = await factory.Member("owner_admin", []);
        var customer = Customer.Create(tenant, "Cliente fictício", "123", null, null, null);
        using (var db = factory.Db(tenant)) { db.Customers.Add(customer); await db.SaveChangesAsync(); }
        var code = "NULL-" + Guid.NewGuid().ToString("N");
        var response = await client.PostAsJsonAsync("/api/v1/quotations", new
        {
            code, customerId = customer.Id, issueDate = DateTime.UtcNow,
            items = nullElement ? new object?[] { null } : null
        });
        await AssertProblemAndContract(client, response, "/api/v1/quotations", "post");
        using (var db = factory.Db(tenant))
        {
            Assert.False(await db.Quotations.AnyAsync(q => q.Code == code));
            Assert.Empty(await db.Set<QuotationItem>().ToListAsync());
            Assert.Equal(customer.Name, (await db.Customers.SingleAsync(c => c.Id == customer.Id)).Name);
        }
    }

    [Theory(DisplayName = "Edição pendente rejeita coleção ou elemento null sem alterar orçamento e itens @spec:AC-037")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateRejectsNullItemsAndPreservesQuotation(bool nullElement)
    {
        var (client, _, tenant) = await factory.Member("owner_admin", []);
        var customer = Customer.Create(tenant, "Cliente fictício", "123", null, null, null);
        var issueDate = DateTime.UtcNow.Date;
        var quotation = Quotation.Create(tenant, "ORIGINAL", customer.Id, issueDate, issueDate.AddDays(7), "Notas originais", "Pagamento original");
        quotation.AddItem(QuotationItem.Create(null, "Serviço original", "un", 20, 2, 0));
        quotation.MarkAsPending();
        var originalItem = quotation.Items.Single();
        using (var db = factory.Db(tenant)) { db.Customers.Add(customer); db.Quotations.Add(quotation); await db.SaveChangesAsync(); }
        var response = await client.PutAsJsonAsync($"/api/v1/quotations/{quotation.Id}", new
        {
            code = "ALTERADO", customerId = customer.Id, issueDate = issueDate.AddDays(1),
            validUntil = issueDate.AddDays(20), notes = "Notas alteradas", paymentTerms = "Pagamento alterado",
            items = nullElement ? new object?[] { null } : null
        });
        await AssertProblemAndContract(client, response, "/api/v1/quotations/{id}", "put");
        using (var db = factory.Db(tenant))
        {
            var saved = await db.Quotations.Include(q => q.Items).SingleAsync(q => q.Id == quotation.Id);
            Assert.Equal(quotation.Code, saved.Code);
            Assert.Equal(quotation.CustomerId, saved.CustomerId);
            Assert.Equal(quotation.IssueDate, saved.IssueDate);
            Assert.Equal(quotation.ValidUntil, saved.ValidUntil);
            Assert.Equal(quotation.Notes, saved.Notes);
            Assert.Equal(quotation.PaymentTerms, saved.PaymentTerms);
            Assert.Equal(quotation.Status, saved.Status);
            Assert.Equal(quotation.TotalAmount, saved.TotalAmount);
            var item = Assert.Single(saved.Items);
            Assert.Equal(originalItem.Id, item.Id);
            Assert.Equal(originalItem.ServiceName, item.ServiceName);
            Assert.Equal(originalItem.Quantity, item.Quantity);
            Assert.Equal(originalItem.UnitPrice, item.UnitPrice);
            Assert.Equal(originalItem.DiscountAmount, item.DiscountAmount);
        }
    }

    private static async Task AssertProblemAndContract(HttpClient client, HttpResponseMessage response, string path, string method)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var text = await response.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(text);
        Assert.Equal(400, problem.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("title").GetString()));
        Assert.DoesNotContain("NullReferenceException", text);
        Assert.DoesNotContain("Quotation must contain", text);
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        Assert.Equal("#/components/schemas/ProblemDetails", document.GetProperty("paths").GetProperty(path).GetProperty(method)
            .GetProperty("responses").GetProperty("400").GetProperty("content").GetProperty("application/problem+json")
            .GetProperty("schema").GetProperty("$ref").GetString());
    }
}
