using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.IntegrationTests.Features;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Agendamento.IntegrationTests.Tenancy;

public sealed class CrossTenantIsolationTests
{
    [Fact(DisplayName = "PostgreSQL isola HTTP e EF e mantém cadastro e autenticação públicos explícitos @spec:AC-028 @spec:AC-029 @spec:AC-030 @spec:AC-031 @spec:AC-032")]
    public async Task PublicRegistrationAndTenantRoutesUseSeparateRestrictedPorts()
    {
        await using var fixture = await PostgreSqlRlsTests.Database.Create(seed: false);
        using var factory = new TeamFinancialTestFactory(fixture.RestrictedConnection, fixture.AdministrativeConnection);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/customers")).StatusCode);
        var registration = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tenants/");
        registration.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        registration.Content = JsonContent.Create(new
        {
            cnpj = "11222333000181", companyName = "Empresa RLS", tradeName = "Empresa RLS", legalNature = "LTDA", primaryCnae = "5611201", categoryId = 1,
            email = "rls@example.test", phone = "11999999999", addressStreet = "Rua", addressNumber = "1", addressComplement = "",
            addressNeighborhood = "Centro", addressCity = "São Paulo", addressState = "SP", addressZipCode = "01000000",
            stateRegistration = "", municipalRegistration = "", isTaxExempt = true, taxRegime = "SimplesNacional", fiscalEmail = "fiscal@example.test",
            adminName = "Administrador RLS", adminCpf = (string?)null, adminEmail = "admin-rls@example.test", adminPassword = "Teste!Local123456", isLegalRepresentative = true
        });
        Assert.Equal(HttpStatusCode.Created, (await anonymous.SendAsync(registration)).StatusCode);
        var b = (Guid)(await PostgreSqlRlsTests.Scalar(fixture.Admin, "SELECT \"Id\" FROM tenants WHERE \"Cnpj\"='11222333000181'"))!;
        // Confirmation is arranged using only the temporary fixture's migration credential.
        await PostgreSqlRlsTests.Execute(fixture.Admin, "UPDATE \"Users\" SET \"EmailVerifiedAtUtc\"=now(), \"Status\"=1 WHERE \"Email\"='admin-rls@example.test'");
        var a = (Guid)(await PostgreSqlRlsTests.Scalar(fixture.Admin, "SELECT \"Id\" FROM tenants WHERE \"Cnpj\"='00000000000191'"))!;
        var (clientA, _, _) = await factory.Member("owner_admin", [], a);
        var (clientB, _, _) = await factory.Member("owner_admin", [], b);
        using (clientA) using (clientB)
        {
            clientA.DefaultRequestHeaders.Add("tenant_id", b.ToString());
            var response = await clientA.PostAsJsonAsync("/api/v1/customers", new { name = "RLS cliente A", phone = "11999999999", tenantId = b, tenant_id = b });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            Assert.Equal(HttpStatusCode.OK, (await clientA.GetAsync($"/api/v1/customers/{id}?tenant_id={b}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await clientB.GetAsync($"/api/v1/customers/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await clientB.PutAsJsonAsync($"/api/v1/customers/{id}", new
                { customerId = id, name = "Alteração indevida", phone = "11999999999" })).StatusCode);
            using var db = factory.Db(a);
            Assert.Equal(a, (await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == id)).TenantId);
            Assert.Equal("RLS cliente A", (await db.Customers.SingleAsync(c => c.Id == id)).Name);
        }

        var tenant = new PostgreSqlRlsTests.Tenant(a);
        var options = new DbContextOptionsBuilder<AgendamentoDbContext>().UseNpgsql(fixture.RestrictedConnection)
            .AddInterceptors(new TenantTransactionInterceptor(tenant, new AdministrativeDatabaseScope()), new TenantSaveChangesInterceptor(tenant)).Options;
        await using var guarded = new AgendamentoDbContext(options, tenant);
        guarded.Customers.Add(Agendamento.Api.Domain.Customers.Customer.Create(b, "Divergente", "11999999999", null, null, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => guarded.SaveChangesAsync());
        await using var restricted = new NpgsqlConnection(fixture.RestrictedConnection);
        await restricted.OpenAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => PostgreSqlRlsTests.Execute(restricted, "SET ROLE agendamento_system_user"));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }
}
