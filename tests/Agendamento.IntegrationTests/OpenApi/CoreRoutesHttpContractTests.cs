using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Agendamento.Api.Domain.Customers;
using Agendamento.Api.Domain.Quotations;
using Agendamento.IntegrationTests.Features;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Agendamento.IntegrationTests.OpenApi;

public sealed class CoreRoutesHttpContractTests : IClassFixture<TeamFinancialTestFactory>
{
    private readonly TeamFinancialTestFactory factory;
    public CoreRoutesHttpContractTests(TeamFinancialTestFactory factory) => this.factory = factory;

    private static async Task<JsonElement> Json(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private static async Task Problem(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("title").GetString()));
        Assert.False(body.TryGetProperty("error", out _));
        Assert.False(body.TryGetProperty("message", out _));
    }
    private static async Task Empty(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Null(response.Content.Headers.ContentType);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }
    private static JsonElement Operation(JsonElement document, string path, string method) =>
        document.GetProperty("paths").GetProperty(path).GetProperty(method);
    private static void ResponseContract(JsonElement document, string path, string method, string status, string? media)
    {
        var response = Operation(document, path, method).GetProperty("responses").GetProperty(status);
        if (media is null) Assert.False(response.TryGetProperty("content", out _));
        else Assert.True(response.GetProperty("content").GetProperty(media).TryGetProperty("schema", out _));
    }

    [Fact(DisplayName = "Clientes e serviços: erros ProblemDetails, criação, edição vazia e tenant externo coincidem com OpenAPI @spec:AC-037")]
    public async Task CustomersAndServicesMatchHttpAndEmptyResponses()
    {
        var (client, _, _) = await factory.Member("owner_admin", []);
        var (other, _, _) = await factory.Member("owner_admin", []);
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        foreach (var resource in new[] { "customers", "services" })
        {
            object valid = resource == "customers" ? new { name = "Cliente fictício", phone = "123" } : new { name = "Serviço fictício", unit = "un", basePrice = 10 };
            object invalid = resource == "customers" ? new { name = "", phone = "123" } : new { name = "", unit = "un", basePrice = -1 };
            var path = $"/api/v1/{resource}";
            await Problem(await client.PostAsJsonAsync(path, invalid));
            var createdResponse = await client.PostAsJsonAsync(path, valid);
            var id = (await Json(createdResponse, HttpStatusCode.Created)).GetProperty("id").GetGuid();
            Assert.EndsWith(id.ToString(), createdResponse.Headers.Location!.ToString());
            await Json(await client.GetAsync($"{path}/{id}"), HttpStatusCode.OK);
            await Json(await client.GetAsync(path), HttpStatusCode.OK);
            object update = resource == "customers" ? new { customerId = id, name = "Cliente editado", phone = "123", preserveDocument = true } : new { name = "Serviço editado", unit = "un", basePrice = 11, isActive = true };
            await Empty(await client.PutAsJsonAsync($"{path}/{id}", update), HttpStatusCode.NoContent);
            await Empty(await other.GetAsync($"{path}/{id}"), HttpStatusCode.NotFound);
            await Empty(await other.PutAsJsonAsync($"{path}/{id}", update), HttpStatusCode.NotFound);
            object badUpdate = resource == "customers" ? new { customerId = id, name = "", phone = "123" } : invalid;
            await Problem(await client.PutAsJsonAsync($"{path}/{id}", badUpdate));
            if (resource == "customers") await Problem(await client.PutAsJsonAsync($"{path}/{id}", new { customerId = Guid.NewGuid(), name = "Cliente", phone = "123" }));
            var contractPath = path;
            ResponseContract(document, contractPath, "post", "201", "application/json");
            ResponseContract(document, contractPath, "post", "400", "application/problem+json");
            ResponseContract(document, path + "/{id}", "put", "204", null);
            ResponseContract(document, path + "/{id}", "put", "400", "application/problem+json");
            ResponseContract(document, path + "/{id}", "put", "404", null);
            ResponseContract(document, path + "/{id}", "get", "404", null);
        }
    }

    [Fact(DisplayName = "Agenda: sucesso tipado, horários inválidos, conflito e edição externa sem corpo documentados @spec:AC-037")]
    public async Task AppointmentsMatchSuccessValidationAndTenantIsolation()
    {
        var (client, user, _) = await factory.Member("owner_admin", []);
        var (other, _, _) = await factory.Member("owner_admin", []);
        var start = DateTime.UtcNow.AddDays(2);
        var valid = new { type = 1, startTime = start, endTime = start.AddHours(1), assignedToUserId = user };
        await Problem(await client.GetAsync("/api/v1/appointments/?startDate=invalid"));
        await Problem(await client.PostAsJsonAsync("/api/v1/appointments/", new { type = 0, startTime = start, endTime = start }));
        await Problem(await client.PostAsJsonAsync("/api/v1/appointments/", new { type = 0, startTime = start, endTime = start.AddHours(1), workOrderId = Guid.NewGuid() }));
        var created = await client.PostAsJsonAsync("/api/v1/appointments/", valid);
        var id = (await Json(created, HttpStatusCode.Created)).GetProperty("id").GetGuid();
        Assert.EndsWith(id.ToString(), created.Headers.Location!.ToString());
        await Problem(await client.PostAsJsonAsync("/api/v1/appointments/", valid));
        var second = await Json(await client.PostAsJsonAsync("/api/v1/appointments/", new { type = 1, startTime = start.AddHours(3), endTime = start.AddHours(4), assignedToUserId = user }), HttpStatusCode.Created);
        await Problem(await client.PutAsJsonAsync($"/api/v1/appointments/{second.GetProperty("id").GetGuid()}", valid));
        var listing = await Json(await client.GetAsync("/api/v1/appointments/"), HttpStatusCode.OK);
        Assert.Contains(listing.EnumerateArray(), row => row.GetProperty("id").GetGuid() == id);
        await Empty(await client.PutAsJsonAsync($"/api/v1/appointments/{id}", valid), HttpStatusCode.NoContent);
        await Problem(await client.PutAsJsonAsync($"/api/v1/appointments/{id}", new { startTime = start, endTime = start }));
        await Empty(await other.PutAsJsonAsync($"/api/v1/appointments/{id}", valid), HttpStatusCode.NotFound);
        var foreignListing = await Json(await other.GetAsync("/api/v1/appointments/"), HttpStatusCode.OK);
        Assert.DoesNotContain(foreignListing.EnumerateArray(), row => row.GetProperty("id").GetGuid() == id);
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        ResponseContract(document, "/api/v1/appointments", "get", "200", "application/json");
        ResponseContract(document, "/api/v1/appointments", "post", "201", "application/json");
        ResponseContract(document, "/api/v1/appointments", "post", "400", "application/problem+json");
        ResponseContract(document, "/api/v1/appointments/{id}", "put", "204", null);
        ResponseContract(document, "/api/v1/appointments/{id}", "put", "404", null);
        ResponseContract(document, "/api/v1/appointments/{id}", "put", "400", "application/problem+json");
    }

    [Fact(DisplayName = "Conversão alternativa e OS: 201 tipado, falhas de domínio seguras, 204/404 vazios e isolamento HTTP @spec:AC-037")]
    public async Task WorkOrdersMatchConversionAndStatusContracts()
    {
        var (client, _, tenant) = await factory.Member("owner_admin", []);
        var (other, _, _) = await factory.Member("owner_admin", []);
        var customer = Customer.Create(tenant, "Cliente fictício", "123", null, null, null);
        var quotation = Quotation.Create(tenant, "Contrato", customer.Id, DateTime.UtcNow, null, null, null);
        quotation.AddItem(QuotationItem.Create(null, "Serviço", "un", 1, 10, 0));
        using (var db = factory.Db(tenant)) { db.Customers.Add(customer); db.Quotations.Add(quotation); await db.SaveChangesAsync(); }
        var conversion = $"/api/v1/work-orders/from-quotation/{quotation.Id}";
        await Problem(await client.PostAsync(conversion, null));
        await Problem(await other.PostAsync(conversion, null));
        using (var db = factory.Db(tenant)) { var saved = await db.Quotations.Include(q => q.Items).SingleAsync(q => q.Id == quotation.Id); saved.MarkAsPending(); saved.Approve(); await db.SaveChangesAsync(); }
        var created = await client.PostAsync(conversion, null);
        var id = (await Json(created, HttpStatusCode.Created)).GetProperty("id").GetGuid();
        Assert.EndsWith(id.ToString(), created.Headers.Location!.ToString());
        Assert.Equal(id, (await Json(await client.PostAsync(conversion, null), HttpStatusCode.Created)).GetProperty("id").GetGuid());
        var detail = await Json(await client.GetAsync($"/api/v1/work-orders/{id}"), HttpStatusCode.OK);
        Assert.Equal(quotation.Id, detail.GetProperty("quotationId").GetGuid());
        await Empty(await other.GetAsync($"/api/v1/work-orders/{id}"), HttpStatusCode.NotFound);
        await Empty(await other.PatchAsJsonAsync($"/api/v1/work-orders/{id}/status", new { status = 1 }), HttpStatusCode.NotFound);
        await Empty(await client.PatchAsJsonAsync($"/api/v1/work-orders/{id}/status", new { status = 4 }), HttpStatusCode.NoContent);
        await Problem(await client.PatchAsJsonAsync($"/api/v1/work-orders/{id}/status", new { status = 1 }));
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        ResponseContract(document, "/api/v1/work-orders/from-quotation/{quotationId}", "post", "201", "application/json");
        ResponseContract(document, "/api/v1/work-orders/from-quotation/{quotationId}", "post", "400", "application/problem+json");
        ResponseContract(document, "/api/v1/work-orders/{id}", "get", "200", "application/json");
        ResponseContract(document, "/api/v1/work-orders/{id}", "get", "404", null);
        ResponseContract(document, "/api/v1/work-orders/{id}/status", "patch", "204", null);
        ResponseContract(document, "/api/v1/work-orders/{id}/status", "patch", "404", null);
        ResponseContract(document, "/api/v1/work-orders/{id}/status", "patch", "400", "application/problem+json");
    }
}
