using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Agendamento.Api.Application.Tenants.CompanyRegistry;
using Agendamento.Domain.Identity;
using Agendamento.IntegrationTests.Features;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Agendamento.IntegrationTests.OpenApi;

public sealed class AuthenticationInputContractTests : IClassFixture<TeamFinancialTestFactory>
{
    private readonly TeamFinancialTestFactory factory;
    public AuthenticationInputContractTests(TeamFinancialTestFactory factory) => this.factory = factory;

    [Fact(DisplayName = "401 sem token, inválido e revogado é vazio; 403 de permissão é ProblemDetails @spec:AC-037")]
    public async Task AuthenticationAndPermissionMatchContract()
    {
        using var anonymous = factory.CreateClient();
        using var document = JsonDocument.Parse(await anonymous.GetStringAsync("/openapi/v1.json"));
        foreach (var path in new[] { "/api/v1/users/me", "/api/v1/tenants/me", "/api/v1/customers", "/api/v1/subscriptions/current" })
        {
            foreach (var invalid in new[] { false, true })
            {
                anonymous.DefaultRequestHeaders.Authorization = invalid ? new AuthenticationHeaderValue("Bearer", "invalid-test-value") : null;
                using var response = await anonymous.GetAsync(path);
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                Assert.Empty(await response.Content.ReadAsByteArrayAsync());
                Assert.Null(response.Content.Headers.ContentType);
                Assert.Contains(response.Headers.WwwAuthenticate, h => h.Scheme == "Bearer");
                var contract = Response(document, path, "get", 401);
                Assert.False(contract.TryGetProperty("content", out _));
                Assert.True(contract.GetProperty("headers").TryGetProperty("WWW-Authenticate", out _));
            }
        }
        var (employee, user, tenant) = await factory.Member("Employee", [Permissions.CustomersRead]);
        using var denied = await employee.GetAsync("/api/v1/financial/receivables");
        await AssertProblem(denied, document, "/api/v1/financial/receivables", "get", 403);
        using (var db = factory.Db(tenant))
        {
            var member = db.TenantMemberships.Single(m => m.UserId == user);
            member.SetActive(false);
            await db.SaveChangesAsync();
        }
        using var revoked = await employee.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        Assert.Empty(await revoked.Content.ReadAsByteArrayAsync());
        Assert.Null(revoked.Content.Headers.ContentType);
    }

    [Fact(DisplayName = "Binding JSON, query e mídia inválidos retornam ProblemDetails seguros documentados @spec:AC-037")]
    public async Task InvalidInputsAreSafeAndDocumented()
    {
        var (client, _, _) = await factory.Member("owner_admin", []);
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        using var malformed = await client.PostAsync("/api/v1/customers", new StringContent("{broken", Encoding.UTF8, "application/json"));
        await AssertProblem(malformed, document, "/api/v1/customers", "post", 400);
        using var unsupported = await client.PostAsync("/api/v1/customers", new StringContent("test", Encoding.UTF8, "text/plain"));
        await AssertProblem(unsupported, document, "/api/v1/customers", "post", 415);
        using var query = await client.GetAsync("/api/v1/financial/statement?startDate=invalid&endDate=invalid");
        await AssertProblem(query, document, "/api/v1/financial/statement", "get", 400);
        using var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/sessions", new { email = "", password = "" });
        await AssertProblem(login, document, "/api/v1/auth/sessions", "post", 400);
    }

    [Theory(DisplayName = "CNPJ HTTP distingue sucesso e falhas, com mídia e schema correspondentes @spec:AC-037")]
    [InlineData("invalid", 400, "active")]
    [InlineData("00000000000191", 404, "missing")]
    [InlineData("00000000000191", 422, "inactive")]
    [InlineData("00000000000191", 503, "unavailable")]
    [InlineData("00000000000191", 200, "active")]
    public async Task RegistryResponsesMatchContract(string cnpj, int status, string outcome)
    {
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ICompanyRegistryGateway>();
            services.AddSingleton<ICompanyRegistryGateway>(new RegistryGateway(outcome));
        }));
        using var client = host.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        using var response = await client.GetAsync($"/api/v1/company-registry/{cnpj}");
        if (status != 200) await AssertProblem(response, document, "/api/v1/company-registry/{cnpj}", "get", status);
        else
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(cnpj, body.GetProperty("cnpj").GetString());
            Assert.Equal("#/components/schemas/CompanyRegistryResult", Response(document, "/api/v1/company-registry/{cnpj}", "get", status)
                .GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString());
        }
    }

    [Fact(DisplayName = "Limite de CNPJ retorna 429 ProblemDetails em vez de 503 vazio @spec:AC-037")]
    public async Task RegistryRateLimitIsDocumented()
    {
        using var isolated = new TeamFinancialTestFactory();
        using var client = isolated.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        for (var i = 0; i < 5; i++)
        {
            using var permitted = await client.GetAsync("/api/v1/company-registry/invalid");
            Assert.Equal(HttpStatusCode.BadRequest, permitted.StatusCode);
        }
        using var rejected = await client.GetAsync("/api/v1/company-registry/invalid");
        await AssertProblem(rejected, document, "/api/v1/company-registry/{cnpj}", "get", 429);
    }

    private static JsonElement Response(JsonDocument doc, string path, string method, int status) => doc.RootElement
        .GetProperty("paths").GetProperty(path).GetProperty(method).GetProperty("responses").GetProperty(status.ToString());

    [Fact(DisplayName = "Cadastro valida header e falha de domínio com ProblemDetails sem credenciais @spec:AC-037")]
    public async Task RegistrationErrorsMatchContract()
    {
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var request = new Agendamento.Api.Features.Tenants.RegisterTenantRequest("invalid", "Empresa fictícia", "Teste", "LTDA", "6204000", 1,
            "company@example.test", "123", "Rua", "1", "", "Centro", "Cidade", "SP", "01000000", "", "", false, "Simples", "fiscal@example.test",
            "Admin fictício", "12345678909", "admin@example.test", "internal-marker-password", true);
        using var missing = await client.PostAsJsonAsync("/api/v1/tenants", request);
        await AssertProblem(missing, document, "/api/v1/tenants", "post", 400);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var invalid = await client.PostAsJsonAsync("/api/v1/tenants", request);
        await AssertProblem(invalid, document, "/api/v1/tenants", "post", 422);
        Assert.DoesNotContain(request.AdminPassword, await invalid.Content.ReadAsStringAsync());
        Assert.DoesNotContain(request.AdminCpf, await invalid.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "Falha de infraestrutura na sessão é 503 seguro conforme contrato @spec:AC-037")]
    public async Task SessionUnavailableMatchesContract()
    {
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<Agendamento.Application.Identity.CreateSession.IIdentityAuthenticationStore>();
            services.AddSingleton<Agendamento.Application.Identity.CreateSession.IIdentityAuthenticationStore>(new UnavailableIdentityStore());
        }));
        using var client = host.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        using var response = await client.PostAsJsonAsync("/api/v1/auth/sessions", new { email = "test@example.test", password = "test" });
        await AssertProblem(response, document, "/api/v1/auth/sessions", "post", 503);
        using var invalid = await client.PostAsJsonAsync("/api/v1/auth/email-verifications", new { token = "invalid-test-value" });
        await AssertProblem(invalid, document, "/api/v1/auth/email-verifications", "post", 400);
        Assert.False(Response(document, "/api/v1/auth/email-verifications", "post", 204).TryGetProperty("content", out _));
    }

    private sealed class UnavailableIdentityStore : Agendamento.Application.Identity.CreateSession.IIdentityAuthenticationStore
    {
        public Task<Agendamento.Application.Identity.CreateSession.AuthenticationIdentity?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
            => throw new TimeoutException("internal-marker");
    }

    [Fact(DisplayName = "Health público declara texto real de sucesso e indisponibilidade sem detalhes internos @spec:AC-036 @spec:AC-037")]
    public async Task HealthResponsesMatchContract()
    {
        using var healthy = factory.CreateClient();
        using var unhealthyHost = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddHealthChecks().AddCheck("contract-test", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("internal-marker"))));
        using var unhealthy = unhealthyHost.CreateClient();
        foreach (var (client, status, text) in new[] { (healthy, 200, "Healthy"), (unhealthy, 503, "Unhealthy") })
        {
            using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
            using var response = await client.GetAsync("/health");
            Assert.Equal(status, (int)response.StatusCode);
            Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal(text, await response.Content.ReadAsStringAsync());
            Assert.Equal("string", Response(document, "/health", "get", status).GetProperty("content").GetProperty("text/plain")
                .GetProperty("schema").GetProperty("type").GetString());
            Assert.False(document.RootElement.GetProperty("paths").GetProperty("/health").GetProperty("get").TryGetProperty("security", out _));
        }
    }

    private static async Task AssertProblem(HttpResponseMessage response, JsonDocument doc, string path, string method, int status)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var text = await response.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(text);
        Assert.Equal(status, problem.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("title").GetString()));
        Assert.DoesNotContain("internal-marker", text);
        Assert.DoesNotContain("invalid-test-value", text);
        Assert.Equal("#/components/schemas/ProblemDetails", Response(doc, path, method, status).GetProperty("content")
            .GetProperty("application/problem+json").GetProperty("schema").GetProperty("$ref").GetString());
    }

    private sealed class RegistryGateway(string outcome) : ICompanyRegistryGateway
    {
        public Task<CompanyRegistryResult?> GetCompanyAsync(string cnpj, CancellationToken cancellationToken = default)
        {
            if (outcome == "unavailable") throw new InvalidOperationException("internal-marker");
            return Task.FromResult<CompanyRegistryResult?>(outcome == "missing" ? null : new CompanyRegistryResult(cnpj,
                "Empresa fictícia", "Teste", "LTDA", ["6204000"], outcome == "inactive" ? "BAIXADA" : "ATIVA",
                new RegistryAddress("Rua", "1", "", "Centro", "Cidade", "SP", "01000000")));
        }
    }

    [Theory(DisplayName = "Erro inesperado HTTP é 500 ProblemDetails seguro também em Development @spec:AC-037")]
    [InlineData("Development")]
    [InlineData("Testing")]
    public async Task UnexpectedFailureDoesNotExposeExceptionsOrAuthorization(string environment)
    {
        var (_, userId, tenant) = await factory.Member("owner_admin", []);
        using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<Agendamento.Api.Application.Quotations.ExportPdf.IQuotationPdfGenerator>();
                services.AddSingleton<Agendamento.Api.Application.Quotations.ExportPdf.IQuotationPdfGenerator>(new FailingPdfGenerator());
            });
        });
        using var client = host.CreateClient();
        string email;
        using (var db = factory.Db(tenant)) email = db.Users.Single(u => u.Id == userId).Email;
        using var login = await client.PostAsJsonAsync("/api/v1/auth/sessions", new { email, password = "Teste!Local123456" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.GetProperty("accessToken").GetString());
        var customer = Agendamento.Api.Domain.Customers.Customer.Create(tenant, "Cliente fictício", "123", null, null, null);
        var quotation = Agendamento.Api.Domain.Quotations.Quotation.Create(tenant, "SAFE", customer.Id, DateTime.UtcNow, null, null, null);
        quotation.AddItem(Agendamento.Api.Domain.Quotations.QuotationItem.Create(null, "Serviço", "un", 10, 1, 0));
        quotation.MarkAsPending();
        using (var db = factory.Db(tenant)) { db.Customers.Add(customer); db.Quotations.Add(quotation); await db.SaveChangesAsync(); }
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        using var response = await client.GetAsync($"/api/v1/quotations/{quotation.Id}/pdf");
        await AssertProblem(response, document, "/api/v1/quotations/{id}/pdf", "get", 500);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain(client.DefaultRequestHeaders.Authorization!.Parameter!, body);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FailingPdfGenerator : Agendamento.Api.Application.Quotations.ExportPdf.IQuotationPdfGenerator
    {
        public Task<byte[]> GeneratePdfAsync(Agendamento.Api.Application.Quotations.GetQuotations.QuotationDetailDto quotation, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("internal-marker");
    }
}
