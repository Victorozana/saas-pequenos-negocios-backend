using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Agendamento.Api.Application.Quotations.CreateQuotation;
using Agendamento.Api.Application.Quotations.GetQuotations;
using Agendamento.Api.Application.Quotations.UpdateQuotation;
using Agendamento.Api.Application.Quotations.UpdateQuotationStatus;
using Agendamento.Api.Domain.Customers;
using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Domain.Services;
using Agendamento.Api.Features.Common;
using Agendamento.IntegrationTests.Features;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Agendamento.IntegrationTests.Tenancy.PostgreSqlRlsTests;

namespace Agendamento.IntegrationTests.Quotations;

public sealed class QuotationEndpointsHttpTests
{
    [Fact(DisplayName = "Endpoints observam permissões, binding, paginação e isolamento HTTP @spec:AC-090 @spec:AC-092 @spec:AC-093")]
    public async Task InMemoryContract()
    {
        using var factory = new TeamFinancialTestFactory();
        await Prove(factory);
    }

    [Fact(DisplayName = "Contrato HTTP de orçamento persiste sem vazamento ou registros parciais em PostgreSQL restrito @spec:AC-090 @spec:AC-092 @spec:AC-093")]
    public async Task PostgreSqlContract()
    {
        await using var database = await Database.Create();
        using var factory = new TeamFinancialTestFactory(database.RestrictedConnection, database.AdministrativeConnection);
        await Prove(factory, database.A, database.B);
    }

    private static async Task Prove(TeamFinancialTestFactory factory, Guid? tenantA = null, Guid? tenantB = null)
    {
        var (owner, _, tenant) = await factory.Member("owner_admin", [], tenantA);
        var (foreign, _, foreignTenant) = await factory.Member("owner_admin", [], tenantB);
        var (reader, _, _) = await factory.Member("Employee", ["sales.read"], tenant);
        var (writer, _, _) = await factory.Member("Employee", ["sales.write"], tenant);
        var (neither, _, _) = await factory.Member("Employee", [], tenant);
        using var anonymous = factory.CreateClient();
        var customer = Customer.Create(tenant, "Cliente BuscaAgulha fictício", "123", null, null, null);
        var externalCustomer = Customer.Create(foreignTenant, "Cliente externo fictício", "123", null, null, null);
        var externalService = ServiceItem.Create(foreignTenant, "Catálogo externo", null, "un", 10);
        using (var db = factory.Db(tenant)) { db.Customers.Add(customer); await db.SaveChangesAsync(); }
        using (var db = factory.Db(foreignTenant)) { db.Customers.Add(externalCustomer); db.ServiceItems.Add(externalService); await db.SaveChangesAsync(); }
        int baselineQuotations, baselineItems;
        using (var db = factory.Db(tenant)) { baselineQuotations = await db.Quotations.CountAsync(); baselineItems = await db.Set<QuotationItem>().CountAsync(); }
        var today = DateTime.UtcNow.Date;
        var command = new CreateQuotationCommand($"HTTP-{Guid.NewGuid():N}", customer.Id, today, today.AddDays(10),
            "Notas fictícias", "Parcelado", [new(null, "Serviço A", "un", 25, 2, 5), new(null, "Serviço B", "hora", 10, 3, 0)],
            new(DepositType.Percentage, 20, "Entrada fictícia"));
        var edit = new UpdateQuotationCommand(command.Code, command.CustomerId, command.IssueDate, command.ValidUntil,
            command.Notes, command.PaymentTerms, command.Items, command.Deposit);
        var missing = Guid.NewGuid();
        async Task<HttpResponseMessage> Operation(HttpClient actor, string operation) => operation switch
        {
            "create" => await actor.PostAsJsonAsync("/api/v1/quotations", command with { CustomerId = Guid.Empty }),
            "list" => await actor.GetAsync("/api/v1/quotations"),
            "detail" => await actor.GetAsync($"/api/v1/quotations/{missing}"),
            "edit" => await actor.PutAsJsonAsync($"/api/v1/quotations/{missing}", edit),
            "status" => await actor.PatchAsJsonAsync($"/api/v1/quotations/{missing}/status", new UpdateQuotationStatusCommand(QuotationStatus.Approved)),
            "pdf" => await actor.GetAsync($"/api/v1/quotations/{missing}/pdf"),
            _ => await actor.PostAsync($"/api/v1/quotations/{missing}/convert", null)
        };
        foreach (var operation in new[] { "create", "list", "detail", "edit", "status", "pdf", "convert" })
        {
            var readOperation = operation is "list" or "detail" or "pdf";
            foreach (var (actor, expected) in new[] {
                (anonymous, HttpStatusCode.Unauthorized), (neither, HttpStatusCode.Forbidden),
                (readOperation ? writer : reader, HttpStatusCode.Forbidden),
                (readOperation ? reader : writer, operation == "list" ? HttpStatusCode.OK : operation is "create" or "convert" ? HttpStatusCode.BadRequest : HttpStatusCode.NotFound) })
            {
                var response = await Operation(actor, operation);
                Assert.Equal(expected, response.StatusCode);
                if (expected == HttpStatusCode.Unauthorized) { Assert.Contains(response.Headers.WwwAuthenticate, h => h.Scheme == "Bearer"); Assert.Equal("", await response.Content.ReadAsStringAsync()); }
                if (expected == HttpStatusCode.Forbidden) await SafeProblem(response, expected);
                if (expected == HttpStatusCode.NotFound) Assert.Equal("", await response.Content.ReadAsStringAsync());
            }
        }
        var spoofed = JsonSerializer.SerializeToNode(command, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
        spoofed["tenantId"] = foreignTenant.ToString();
        var created = await writer.PostAsJsonAsync("/api/v1/quotations", spoofed);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("application/json", created.Content.Headers.ContentType!.MediaType);
        var id = (await created.Content.ReadFromJsonAsync<CreatedIdResponse>())!.Id;
        Assert.Equal($"/api/v1/quotations/{id}", created.Headers.Location!.OriginalString);
        var detailResponse = await reader.GetAsync(created.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.Equal("application/json", detailResponse.Content.Headers.ContentType!.MediaType);
        var detailBody = await detailResponse.Content.ReadAsStringAsync();
        var detail = JsonSerializer.Deserialize<QuotationDetailDto>(detailBody, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var detailJson = JsonSerializer.Deserialize<JsonElement>(detailBody);
        Assert.False(detailJson.TryGetProperty("tenantId", out _)); Assert.False(detailJson.TryGetProperty("documentValue", out _));
        Assert.Equal(customer.Id, detail.CustomerId); Assert.Equal(customer.Name, detail.CustomerName);
        Assert.Equal(command.Code, detail.Code); Assert.Equal(today, detail.IssueDate); Assert.Equal(command.ValidUntil, detail.ValidUntil);
        Assert.Equal(command.Notes, detail.Notes); Assert.Equal(command.PaymentTerms, detail.PaymentTerms);
        Assert.Equal(QuotationStatus.Pending, detail.Status); Assert.Equal(80, detail.SubtotalAmount);
        Assert.Equal(5, detail.DiscountAmount); Assert.Equal(75, detail.TotalAmount);
        Assert.Equal(2, detail.Items.Count); Assert.All(detail.Items, item => Assert.NotEqual(Guid.Empty, item.Id));
        Assert.Equal(new[] { 30m, 45m }, detail.Items.Select(item => item.TotalPrice).Order());
        foreach (var input in command.Items)
        {
            var item = Assert.Single(detail.Items, i => i.ServiceName == input.ServiceName);
            Assert.Equal(input.ServiceItemId, item.ServiceItemId); Assert.Equal(input.Unit, item.Unit);
            Assert.Equal(input.UnitPrice, item.UnitPrice); Assert.Equal(input.Quantity, item.Quantity); Assert.Equal(input.DiscountAmount, item.DiscountAmount);
        }
        Assert.Equal(15, detail.DepositInfo!.RequiredAmount); Assert.Equal(60, detail.DepositInfo.RemainingBalance);
        Assert.Equal(20, detail.DepositInfo.Value); Assert.Equal(DepositType.Percentage, detail.DepositInfo.Type);
        Assert.Equal(command.Deposit!.PaymentNotes, detail.DepositInfo.PaymentNotes); Assert.False(detail.DepositInfo.IsPaid);
        using (var db = factory.Db(tenant)) Assert.Equal(tenant, (await db.Quotations.SingleAsync(q => q.Id == id)).TenantId);
        var snapshot = await reader.GetStringAsync($"/api/v1/quotations/{id}");
        using (var spoofRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/quotations/{id}?tenantId={foreignTenant}"))
        {
            spoofRequest.Headers.Add("X-Tenant-Id", foreignTenant.ToString());
            Assert.Equal(snapshot, await (await reader.SendAsync(spoofRequest)).Content.ReadAsStringAsync());
        }
        using (var spoofRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/quotations/{id}?tenantId={tenant}"))
        {
            spoofRequest.Headers.Add("X-Tenant-Id", tenant.ToString());
            Assert.Equal(HttpStatusCode.NotFound, (await foreign.SendAsync(spoofRequest)).StatusCode);
        }
        async Task AssertUnchanged()
        {
            Assert.Equal(snapshot, await reader.GetStringAsync($"/api/v1/quotations/{id}"));
            using var db = factory.Db(tenant);
            Assert.Equal(baselineItems + 2, await db.Set<QuotationItem>().CountAsync());
            Assert.Empty(await db.ReceivableTitles.Where(r => r.QuotationId == id).ToListAsync()); Assert.Empty(await db.WorkOrders.Where(w => w.QuotationId == id).ToListAsync());
        }
        foreach (var suffix in new[] { "", "/pdf" }) Assert.Equal(HttpStatusCode.NotFound, (await foreign.GetAsync($"/api/v1/quotations/{id}{suffix}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await foreign.PutAsJsonAsync($"/api/v1/quotations/{id}", edit)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await foreign.PatchAsJsonAsync($"/api/v1/quotations/{id}/status", new { newStatus = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await foreign.PostAsync($"/api/v1/quotations/{id}/convert", null)).StatusCode);
        Assert.DoesNotContain(id, (await foreign.GetFromJsonAsync<PagedResult<QuotationListDto>>("/api/v1/quotations"))!.Items.Select(q => q.Id));
        await AssertUnchanged();
        foreach (var verb in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Patch })
        {
            var path = verb == HttpMethod.Post ? "/api/v1/quotations" : $"/api/v1/quotations/{id}" + (verb == HttpMethod.Patch ? "/status" : "");
            foreach (var raw in new[] { "null", "{", "[]" })
            {
                var response = await writer.SendAsync(new HttpRequestMessage(verb, path) { Content = new StringContent(raw, Encoding.UTF8, "application/json") });
                await SafeProblem(response);
                await AssertUnchanged();
            }
            var unsupported = await writer.SendAsync(new HttpRequestMessage(verb, path) { Content = new StringContent("{}", Encoding.UTF8, "text/plain") });
            await SafeProblem(unsupported, HttpStatusCode.UnsupportedMediaType);
            await AssertUnchanged();
            Assert.Equal(HttpStatusCode.BadRequest, (await writer.SendAsync(new HttpRequestMessage(verb, path))).StatusCode);
            await AssertUnchanged();
        }
        foreach (var malformed in new[] { "{\"newStatus\":null}", "{\"newStatus\":\"invalid\"}", "{\"newStatus\":2147483648}" })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await writer.PatchAsync($"/api/v1/quotations/{id}/status", new StringContent(malformed, Encoding.UTF8, "application/json"))).StatusCode);
            await AssertUnchanged();
        }
        foreach (var suffix in new[] { "", "/pdf" }) Assert.Equal(HttpStatusCode.NotFound, (await reader.GetAsync("/api/v1/quotations/not-a-guid" + suffix)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await writer.PutAsJsonAsync("/api/v1/quotations/not-a-guid", edit)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await writer.PatchAsJsonAsync("/api/v1/quotations/not-a-guid/status", new UpdateQuotationStatusCommand(QuotationStatus.Approved))).StatusCode);
        foreach (var invalid in new[] { command with { Items = null! }, command with { Items = [] }, command with { Items = [null!] },
            command with { CustomerId = externalCustomer.Id }, command with { CustomerId = missing },
            command with { Items = [new(missing, "Inválido", "un", 1, 1, 0)] }, command with { Deposit = new((DepositType)999, 1, null) } })
        {
            await SafeProblem(await writer.PostAsJsonAsync("/api/v1/quotations", invalid));
            await AssertUnchanged();
            using var db = factory.Db(tenant); Assert.Equal(baselineQuotations + 1, await db.Quotations.CountAsync());
        }
        await SafeProblem(await writer.PostAsJsonAsync("/api/v1/quotations", command with { Items = [new(externalService.Id, "Externo", "un", 10, 1, 0)] }));
        await SafeProblem(await writer.PutAsJsonAsync($"/api/v1/quotations/{id}", edit with { Items = [new(externalService.Id, "Externo", "un", 10, 1, 0)] }));
        await AssertUnchanged();
        foreach (var status in new[] { -1, 999, (int)QuotationStatus.Draft, (int)QuotationStatus.Converted })
        {
            await SafeProblem(await writer.PatchAsJsonAsync($"/api/v1/quotations/{id}/status", new { newStatus = status }));
            await AssertUnchanged();
        }
        await SafeProblem(await writer.PutAsJsonAsync($"/api/v1/quotations/{id}", edit with { Items = [] }));
        await AssertUnchanged();
        foreach (var invalid in new[] { command with { Code = new string('c', 51) }, command with { Notes = new string('n', 2001) },
            command with { PaymentTerms = new string('p', 2001) }, command with { Deposit = new(DepositType.Percentage, 20, new string('d', 1001)) },
            command with { IssueDate = default }, command with { IssueDate = DateTime.SpecifyKind(today, DateTimeKind.Unspecified) },
            command with { ValidUntil = DateTime.SpecifyKind(today.AddDays(10), DateTimeKind.Unspecified) } })
        {
            await SafeProblem(await writer.PostAsJsonAsync("/api/v1/quotations", invalid));
            await SafeProblem(await writer.PutAsJsonAsync($"/api/v1/quotations/{id}", new UpdateQuotationCommand(
                invalid.Code, invalid.CustomerId, invalid.IssueDate, invalid.ValidUntil, invalid.Notes, invalid.PaymentTerms, invalid.Items, invalid.Deposit)));
            await AssertUnchanged();
        }
        await SafeProblem(await writer.PostAsJsonAsync("/api/v1/quotations", command));
        await AssertUnchanged();
        foreach (var field in new[] { "issueDate", "validUntil" })
        {
            var nonUtc = JsonSerializer.SerializeToNode(command, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
            nonUtc[field] = "2026-10-13T00:00:00-03:00";
            await SafeProblem(await writer.PostAsJsonAsync("/api/v1/quotations", nonUtc));
            await SafeProblem(await writer.PutAsJsonAsync($"/api/v1/quotations/{id}", nonUtc));
            await AssertUnchanged();
        }
        var missingDate = JsonSerializer.SerializeToNode(command, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
        missingDate.Remove("issueDate");
        await SafeProblem(await writer.PostAsJsonAsync("/api/v1/quotations", missingDate));
        await SafeProblem(await writer.PutAsJsonAsync($"/api/v1/quotations/{id}", missingDate));
        await AssertUnchanged();
        var foreignCreated = await foreign.PostAsJsonAsync("/api/v1/quotations", command with { CustomerId = externalCustomer.Id });
        Assert.Equal(HttpStatusCode.Created, foreignCreated.StatusCode);
        await AssertUnchanged();
        Assert.Equal(HttpStatusCode.NoContent, (await writer.PutAsJsonAsync($"/api/v1/quotations/{id}", edit with { Notes = "Edição válida" })).StatusCode);
        Assert.Equal("Edição válida", (await reader.GetFromJsonAsync<QuotationDetailDto>($"/api/v1/quotations/{id}"))!.Notes);
        await ProveListing(owner, customer.Id, today, id, baselineQuotations);
        var approval = await writer.PatchAsJsonAsync($"/api/v1/quotations/{id}/status", new { newStatus = (int)QuotationStatus.Approved });
        Assert.Equal(HttpStatusCode.NoContent, approval.StatusCode); Assert.Equal("", await approval.Content.ReadAsStringAsync());
        snapshot = await reader.GetStringAsync($"/api/v1/quotations/{id}");
        using (var db = factory.Db(tenant)) Assert.Equal(2, await db.ReceivableTitles.CountAsync(r => r.QuotationId == id));
        Assert.Equal(HttpStatusCode.NoContent, (await writer.PatchAsJsonAsync($"/api/v1/quotations/{id}/status", new { newStatus = (int)QuotationStatus.Approved })).StatusCode);
        await SafeProblem(await writer.PatchAsJsonAsync($"/api/v1/quotations/{id}/status", new { newStatus = (int)QuotationStatus.Rejected }));
        await SafeProblem(await writer.PutAsJsonAsync($"/api/v1/quotations/{id}", edit));
        Assert.Equal(snapshot, await reader.GetStringAsync($"/api/v1/quotations/{id}"));
        using (var db = factory.Db(tenant)) { Assert.Equal(2, await db.ReceivableTitles.CountAsync(r => r.QuotationId == id)); Assert.Empty(await db.WorkOrders.Where(w => w.QuotationId == id).ToListAsync()); }
        var boundary = command with { Code = new string('c', 50), Notes = new string('n', 2000), PaymentTerms = new string('p', 2000), Deposit = new(DepositType.Percentage, 20, new string('d', 1000)) };
        var boundaryResponse = await writer.PostAsJsonAsync("/api/v1/quotations", boundary);
        Assert.Equal(HttpStatusCode.Created, boundaryResponse.StatusCode);
        var boundaryId = (await boundaryResponse.Content.ReadFromJsonAsync<CreatedIdResponse>())!.Id;
        var boundaryDetail = (await reader.GetFromJsonAsync<QuotationDetailDto>($"/api/v1/quotations/{boundaryId}"))!;
        Assert.Equal(boundary.Code, boundaryDetail.Code); Assert.Equal(boundary.Notes, boundaryDetail.Notes);
        Assert.Equal(boundary.PaymentTerms, boundaryDetail.PaymentTerms); Assert.Equal(boundary.Deposit!.PaymentNotes, boundaryDetail.DepositInfo!.PaymentNotes);
        var boundaryEdit = new UpdateQuotationCommand(boundary.Code, customer.Id, today, command.ValidUntil, boundary.Notes, boundary.PaymentTerms, boundary.Items, boundary.Deposit);
        Assert.Equal(HttpStatusCode.NoContent, (await writer.PutAsJsonAsync($"/api/v1/quotations/{boundaryId}", boundaryEdit)).StatusCode);
        var boundarySnapshot = await reader.GetStringAsync($"/api/v1/quotations/{boundaryId}");
        await SafeProblem(await writer.PutAsJsonAsync($"/api/v1/quotations/{boundaryId}", boundaryEdit with { Code = command.Code }));
        Assert.Equal(boundarySnapshot, await reader.GetStringAsync($"/api/v1/quotations/{boundaryId}"));
        using (var db = factory.Db(tenant)) Assert.Equal(baselineQuotations + 6, await db.Quotations.CountAsync());
    }

    private static async Task ProveListing(HttpClient owner, Guid customer, DateTime today, Guid original, int baselineQuotations)
    {
        var ids = new List<Guid>();
        for (var i = 0; i < 4; i++)
        {
            var response = await owner.PostAsJsonAsync("/api/v1/quotations", new CreateQuotationCommand($"LIST-Needle-{i}-{Guid.NewGuid():N}", customer,
                today.AddDays(1), null, null, null, [new(null, "Listagem", "un", 1, 1, 0)], null));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            ids.Add((await response.Content.ReadFromJsonAsync<CreatedIdResponse>())!.Id);
        }
        async Task<PagedResult<QuotationListDto>> List(string query) => (await owner.GetFromJsonAsync<PagedResult<QuotationListDto>>("/api/v1/quotations" + query +
            (query.Contains("SearchTerm=") ? "" : (query.Length == 0 ? "?" : "&") + "SearchTerm=BUSCAAGULHA")))!;
        var defaults = await List(""); Assert.Equal(1, defaults.Page); Assert.Equal(20, defaults.PageSize); Assert.Equal(5, defaults.TotalCount);
        var first = await List("?Page=1&PageSize=2"); var second = await List("?Page=2&PageSize=2");
        Assert.Equal(5, first.TotalCount); Assert.Equal(2, first.PageSize); Assert.Equal(2, second.Page);
        Assert.Equal(ids.Order().Take(2), first.Items.Select(q => q.Id));
        Assert.Equal(ids.Order().Skip(2), second.Items.Select(q => q.Id));
        Assert.Equal(first.Items.Select(q => q.Id), (await List("?Page=1&PageSize=2")).Items.Select(q => q.Id));
        Assert.Equal(original, Assert.Single((await List("?Page=3&PageSize=2")).Items).Id);
        Assert.Empty((await List("?Page=4&PageSize=2")).Items);
        Assert.Equal(4, (await List("?SearchTerm=NEEDLE")).TotalCount);
        Assert.Equal(5, (await List("?SearchTerm=BUSCAAGULHA")).TotalCount);
        Assert.Equal(baselineQuotations + 5, (await List("?SearchTerm=%20%20")).TotalCount);
        Assert.Empty((await List("?SearchTerm=ausente")).Items);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PatchAsJsonAsync($"/api/v1/quotations/{ids[0]}/status", new { newStatus = (int)QuotationStatus.Rejected })).StatusCode);
        var rejectedSnapshot = await owner.GetStringAsync($"/api/v1/quotations/{ids[0]}");
        await SafeProblem(await owner.PatchAsJsonAsync($"/api/v1/quotations/{ids[0]}/status", new UpdateQuotationStatusCommand(QuotationStatus.Rejected)));
        Assert.Equal(rejectedSnapshot, await owner.GetStringAsync($"/api/v1/quotations/{ids[0]}"));
        var rejected = await List($"?Status={(int)QuotationStatus.Rejected}&SearchTerm=needle&PageSize=1");
        Assert.Equal(1, rejected.TotalCount); Assert.Equal(ids[0], Assert.Single(rejected.Items).Id);
        Assert.Equal(4, (await List($"?Status={(int)QuotationStatus.Pending}")).TotalCount);
        Assert.Empty((await List("?Page=2147483647&PageSize=1")).Items);
        Assert.Equal(5, (await List("?PageSize=100")).TotalCount);
        foreach (var query in new[] { "?Page=0", "?Page=-1", "?PageSize=0", "?PageSize=-1", "?PageSize=101", "?Page=2147483647&PageSize=2", "?Status=999", "?Status=-1" })
            await SafeProblem(await owner.GetAsync("/api/v1/quotations" + query));
        foreach (var query in new[] { "?Page=wat", "?Page=2147483648", "?Status=wat" })
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync("/api/v1/quotations" + query)).StatusCode);
    }

    private static async Task SafeProblem(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.BadRequest)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)expected, json.GetProperty("status").GetInt32()); Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("title").GetString()));
        Assert.False(json.TryGetProperty("exception", out _)); Assert.False(json.TryGetProperty("stackTrace", out _));
        Assert.False(json.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String && detail.GetString()!.Contains("Npgsql", StringComparison.OrdinalIgnoreCase));
    }
}
