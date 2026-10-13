using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Agendamento.Api.Application.Quotations.CreateQuotation;
using Agendamento.Api.Application.Quotations.UpdateQuotation;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Customers;
using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.IntegrationTests.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Agendamento.IntegrationTests.Tenancy.PostgreSqlRlsTests;

namespace Agendamento.IntegrationTests.Quotations;

public sealed class QuotationEndpointAtomicityTests
{
    [Fact(DisplayName = "OpenAPI live documenta Location de criação e ProblemDetails de consulta inválida @spec:AC-037 @spec:AC-090")]
    public async Task LiveOpenApiMatchesCreationHeaderAndInvalidListing()
    {
        using var factory = new TeamFinancialTestFactory();
        var (client, _, tenant) = await factory.Member("owner_admin", []);
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        var operations = document.GetProperty("paths").GetProperty("/api/v1/quotations");
        var header = operations.GetProperty("post").GetProperty("responses").GetProperty("201")
            .GetProperty("headers").GetProperty("Location");
        Assert.Equal("string", header.GetProperty("schema").GetProperty("type").GetString());
        Assert.True(operations.GetProperty("get").GetProperty("responses").GetProperty("400")
            .GetProperty("content").GetProperty("application/problem+json").TryGetProperty("schema", out _));
        var invalid = await client.GetAsync("/api/v1/quotations?Page=0");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("application/problem+json", invalid.Content.Headers.ContentType!.MediaType);
        var customer = Customer.Create(tenant, "contract customer", "123", null, null, null);
        using (var db = factory.Db(tenant)) { db.Customers.Add(customer); await db.SaveChangesAsync(); }
        var created = await client.PostAsJsonAsync("/api/v1/quotations", Command(customer.Id));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal($"/api/v1/quotations/{id}", created.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(created.Headers.Location)).StatusCode);
    }

    private sealed class ControlledFailure : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var quotation = eventData.Context!.ChangeTracker.Entries<Quotation>()
                .FirstOrDefault(e => e.State == EntityState.Added && e.Entity.Code.StartsWith("controlled-"));
            if (quotation != null)
                throw new DbUpdateException("PRIVATE-PERSISTENCE-MESSAGE", new Npgsql.PostgresException(
                    "PRIVATE-DATABASE-MESSAGE", "ERROR", "ERROR",
                    quotation.Entity.Code.EndsWith("unique") ? Npgsql.PostgresErrorCodes.UniqueViolation : Npgsql.PostgresErrorCodes.ForeignKeyViolation,
                    constraintName: quotation.Entity.Code.EndsWith("unique") ? "unrelated_unique_constraint" : "IX_Quotations_tenant_id_Code"));
            return ValueTask.FromResult(result);
        }
    }

    [Fact(DisplayName = "HTTP não mascara constraint alheia ou FK como código duplicado e preserva persistência @spec:AC-037 @spec:AC-090")]
    public async Task OtherPersistenceFailuresRemainSecureServerErrors()
    {
        using var factory = new TeamFinancialTestFactory();
        var (_, user, tenant) = await factory.Member("owner_admin", []);
        var customer = Customer.Create(tenant, "controlled customer", "123", null, null, null);
        string email;
        using (var db = factory.Db(tenant))
        { db.Customers.Add(customer); await db.SaveChangesAsync(); email = (await db.Users.SingleAsync(u => u.Id == user)).Email; }
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<AgendamentoDbContext>((_, options) => options.AddInterceptors(new ControlledFailure()))));
        using var client = host.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/sessions", new { email, password = "Teste!Local123456" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString());
        foreach (var code in new[] { "controlled-unique", "controlled-fk" })
        {
            var response = await client.PostAsJsonAsync("/api/v1/quotations", Command(customer.Id, code));
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(500, body.GetProperty("status").GetInt32());
            Assert.Equal("Não foi possível concluir a operação. Tente novamente mais tarde.", body.GetProperty("title").GetString());
            Assert.False(body.TryGetProperty("detail", out _));
            Assert.DoesNotContain("PRIVATE", body.GetRawText());
            using var db = factory.Db(tenant);
            Assert.Equal(0, await db.Quotations.CountAsync()); Assert.Equal(0, await db.Set<QuotationItem>().CountAsync());
            Assert.Equal(0, await db.ReceivableTitles.CountAsync()); Assert.Equal(0, await db.WorkOrders.CountAsync());
        }
    }

    private sealed class Tenant(Guid id) : ITenantContext
    {
        public Guid TenantId => id;
        public bool HasTenant => true;
    }

    private static CreateQuotationCommand Command(Guid customer, string code = "original") =>
        new(code, customer, DateTime.UtcNow.Date, null, "original notes", "terms",
            [new(null, "service", "un", 100, 2, 5)], new(DepositType.Percentage, 25, "deposit"));

    [Fact(DisplayName = "Criação e edição recusadas preservam entradas rastreadas e SaveChanges posterior @spec:AC-090 @spec:AC-092 @spec:AC-206")]
    public async Task InvalidInputsLeaveTrackedAggregateUnchanged()
    {
        using var factory = new TeamFinancialTestFactory();
        var (_, _, tenantId) = await factory.Member("owner_admin", []);
        using var db = factory.Db(tenantId);
        var customer = Customer.Create(tenantId, "customer", "123", null, null, null);
        db.Customers.Add(customer); await db.SaveChangesAsync();
        var tenant = new Tenant(tenantId);
        var create = new CreateQuotationHandler(db, tenant);
        var valid = Command(customer.Id);
        var id = await create.HandleAsync(valid);
        await create.HandleAsync(valid with { Code = "duplicate" });
        var quotation = await db.Quotations.Include(q => q.Items).SingleAsync(q => q.Id == id);
        var baseline = JsonSerializer.Serialize(quotation);
        var originalEntries = db.ChangeTracker.Entries().Select(e => e.Entity).ToHashSet();
        var invalid = new[] {
            valid with { Code = new string('x', 51) },
            valid with { Notes = new string('x', 2001) },
            valid with { PaymentTerms = new string('x', 2001) },
            valid with { Deposit = new(DepositType.Percentage, 25, new string('x', 1001)) },
            valid with { IssueDate = DateTime.SpecifyKind(valid.IssueDate, DateTimeKind.Unspecified) },
            valid with { ValidUntil = DateTime.SpecifyKind(valid.IssueDate.AddDays(1), DateTimeKind.Local) },
            valid with { Code = "duplicate" }
        };
        foreach (var candidate in invalid)
        {
            await Assert.ThrowsAsync<ArgumentException>(() => create.HandleAsync(candidate));
            var update = new UpdateQuotationCommand(candidate.Code, candidate.CustomerId, candidate.IssueDate,
                candidate.ValidUntil, candidate.Notes, candidate.PaymentTerms, candidate.Items, candidate.Deposit);
            await Assert.ThrowsAsync<ArgumentException>(() => new UpdateQuotationHandler(db, tenant).HandleAsync(id, update));
            Assert.Equal(baseline, JsonSerializer.Serialize(quotation));
            Assert.True(originalEntries.SetEquals(db.ChangeTracker.Entries().Select(e => e.Entity)));
            Assert.All(db.ChangeTracker.Entries(), e => Assert.Equal(EntityState.Unchanged, e.State));
            Assert.Equal(0, await db.SaveChangesAsync());
            Assert.Equal(2, await db.Quotations.CountAsync());
            Assert.Equal(2, await db.Set<QuotationItem>().CountAsync());
            Assert.Empty(await db.ReceivableTitles.ToListAsync());
            Assert.Empty(await db.WorkOrders.ToListAsync());
        }
    }

    private sealed class SaveBarrier(string code, EntityState state = EntityState.Added) : SaveChangesInterceptor
    {
        private int arrivals;
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Quotation>().Any(e => e.State == state && e.Entity.Code == code))
            {
                if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult();
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
        public int Arrivals => arrivals;
    }

    [Fact(DisplayName = "PUTs concorrentes para código único preservam perdedor e conteúdo completo sem gravação parcial PostgreSQL @spec:AC-206 @spec:AC-092")]
    public async Task ConcurrentEditsKeepLosingDatabaseSnapshotIntact()
    {
        await using var database = await Database.Create();
        using var factory = new TeamFinancialTestFactory(database.RestrictedConnection, database.AdministrativeConnection);
        var (originalClient, userId, tenant) = await factory.Member("owner_admin", [], database.A);
        var customer = Customer.Create(tenant, "edit race customer", "123", null, null, null);
        using (var seed = factory.Db(tenant)) { seed.Customers.Add(customer); await seed.SaveChangesAsync(); }
        var ids = new List<Guid>();
        var snapshots = new List<string>();
        for (var i = 0; i < 2; i++)
        {
            var created = await originalClient.PostAsJsonAsync("/api/v1/quotations", Command(customer.Id, $"edit-source-{i}"));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            ids.Add(id); snapshots.Add(await originalClient.GetStringAsync($"/api/v1/quotations/{id}"));
        }
        int quoteCount, itemCount, receivableCount, workOrderCount;
        using (var baseline = factory.Db(tenant))
        {
            quoteCount = await baseline.Quotations.CountAsync(); itemCount = await baseline.Set<QuotationItem>().CountAsync();
            receivableCount = await baseline.ReceivableTitles.CountAsync(); workOrderCount = await baseline.WorkOrders.CountAsync();
        }
        var target = "edit-target-" + Guid.NewGuid().ToString("N");
        var barrier = new SaveBarrier(target, EntityState.Modified);
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<AgendamentoDbContext>((_, options) => options.AddInterceptors(barrier))));
        using var client = host.CreateClient();
        string email;
        using (var loginDb = factory.Db(tenant)) email = (await loginDb.Users.SingleAsync(u => u.Id == userId)).Email;
        var login = await client.PostAsJsonAsync("/api/v1/auth/sessions", new { email, password = "Teste!Local123456" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString());
        var edit = new UpdateQuotationCommand(target, customer.Id, DateTime.UtcNow.Date, null, "race edited notes", "new terms",
            [new(null, "replacement A", "hora", 12, 2, 1), new(null, "replacement B", "un", 10, 1, 0)],
            new(DepositType.FixedAmount, 10, "new deposit notes"));
        var responses = await Task.WhenAll(ids.Select(id => client.PutAsJsonAsync($"/api/v1/quotations/{id}", edit)));
        Assert.Equal(2, barrier.Arrivals);
        var success = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.NoContent);
        Assert.Empty(await success.Content.ReadAsByteArrayAsync()); Assert.Null(success.Content.Headers.ContentType);
        var failure = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.BadRequest);
        Assert.Equal("application/problem+json", failure.Content.Headers.ContentType!.MediaType);
        var problem = await failure.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.Equal("Não foi possível editar o orçamento. Verifique os dados e o estado atual.", problem.GetProperty("title").GetString());
        Assert.False(problem.TryGetProperty("detail", out _));
        var winner = Array.FindIndex(responses, r => r.StatusCode == HttpStatusCode.NoContent);
        var loser = 1 - winner;
        // HTTP request scopes are disposed after relational failures. This checks
        // committed state; tracked preservation is separately proved for prevalidation.
        Assert.Equal(snapshots[loser], await originalClient.GetStringAsync($"/api/v1/quotations/{ids[loser]}"));
        using var db = factory.Db(tenant);
        var saved = await db.Quotations.Include(q => q.Items).SingleAsync(q => q.Id == ids[winner]);
        Assert.Equal(target, saved.Code); Assert.Equal(QuotationStatus.Pending, saved.Status);
        Assert.Equal(edit.Notes, saved.Notes); Assert.Equal(edit.PaymentTerms, saved.PaymentTerms);
        Assert.Equal(edit.CustomerId, saved.CustomerId); Assert.Equal(edit.IssueDate, saved.IssueDate); Assert.Null(saved.ValidUntil);
        Assert.Equal(34, saved.SubtotalAmount); Assert.Equal(1, saved.DiscountAmount); Assert.Equal(33, saved.TotalAmount);
        Assert.Equal(DepositType.FixedAmount, saved.DepositInfo!.Type); Assert.Equal(10, saved.DepositInfo.Value);
        Assert.Equal(10, saved.DepositInfo.RequiredAmount); Assert.Equal(23, saved.DepositInfo.RemainingBalance);
        Assert.Equal(edit.Deposit!.PaymentNotes, saved.DepositInfo.PaymentNotes); Assert.False(saved.DepositInfo.IsPaid);
        Assert.Equal(2, saved.Items.Count);
        foreach (var expected in edit.Items)
        {
            var item = Assert.Single(saved.Items, i => i.ServiceName == expected.ServiceName);
            Assert.Equal(expected.Unit, item.Unit); Assert.Equal(expected.UnitPrice, item.UnitPrice);
            Assert.Equal(expected.Quantity, item.Quantity); Assert.Equal(expected.DiscountAmount, item.DiscountAmount);
            Assert.Null(item.ServiceItemId);
        }
        Assert.Equal(quoteCount, await db.Quotations.CountAsync()); Assert.Equal(itemCount + 1, await db.Set<QuotationItem>().CountAsync());
        Assert.Equal(1, await db.Quotations.CountAsync(q => q.Code == target));
        Assert.Equal(receivableCount, await db.ReceivableTitles.CountAsync()); Assert.Equal(workOrderCount, await db.WorkOrders.CountAsync());
    }

    [Fact(DisplayName = "Corrida HTTP de código único produz 201 e 400 seguro sem orçamento ou itens parciais PostgreSQL @spec:AC-090 @spec:AC-092")]
    public async Task ConcurrentCreatesRespectUniqueCodeWithoutPartialRows()
    {
        await using var database = await Database.Create();
        using var factory = new TeamFinancialTestFactory(database.RestrictedConnection, database.AdministrativeConnection);
        var (_, userId, tenant) = await factory.Member("owner_admin", [], database.A);
        var customer = Customer.Create(tenant, "race customer", "123", null, null, null);
        using (var seed = factory.Db(tenant)) { seed.Customers.Add(customer); await seed.SaveChangesAsync(); }
        var command = Command(customer.Id, "race-" + Guid.NewGuid().ToString("N"));
        int originalQuotationCount, originalItemCount, originalReceivableCount, originalWorkOrderCount;
        using (var baselineDb = factory.Db(tenant))
        {
            originalQuotationCount = await baselineDb.Quotations.CountAsync();
            originalItemCount = await baselineDb.Set<QuotationItem>().CountAsync();
            originalReceivableCount = await baselineDb.ReceivableTitles.CountAsync();
            originalWorkOrderCount = await baselineDb.WorkOrders.CountAsync();
        }
        var barrier = new SaveBarrier(command.Code);
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<AgendamentoDbContext>((_, options) => options.AddInterceptors(barrier))));
        using var racingClient = host.CreateClient();
        string email;
        using (var loginDb = factory.Db(tenant)) email = (await loginDb.Users.SingleAsync(u => u.Id == userId)).Email;
        var login = await racingClient.PostAsJsonAsync("/api/v1/auth/sessions", new { email, password = "Teste!Local123456" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await login.Content.ReadFromJsonAsync<JsonElement>();
        racingClient.DefaultRequestHeaders.Authorization = new("Bearer", session.GetProperty("accessToken").GetString());
        var responses = await Task.WhenAll(racingClient.PostAsJsonAsync("/api/v1/quotations", command),
            racingClient.PostAsJsonAsync("/api/v1/quotations", command));
        Assert.Equal(2, barrier.Arrivals);
        var success = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        var failure = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.BadRequest);
        var id = (await success.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal($"/api/v1/quotations/{id}", success.Headers.Location!.ToString());
        Assert.Equal("application/problem+json", failure.Content.Headers.ContentType!.MediaType);
        var problem = await failure.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.Equal("Não foi possível criar o orçamento. Verifique os dados informados.", problem.GetProperty("title").GetString());
        Assert.False(problem.TryGetProperty("detail", out _));
        using var db = factory.Db(tenant);
        var saved = Assert.Single(await db.Quotations.Include(q => q.Items).Where(q => q.Code == command.Code).ToListAsync());
        Assert.Equal(id, saved.Id); Assert.Equal(QuotationStatus.Pending, saved.Status);
        Assert.Equal(195, saved.TotalAmount); Assert.Equal(48.75m, saved.DepositInfo!.RequiredAmount);
        Assert.Single(saved.Items);
        Assert.Equal(originalQuotationCount + 1, await db.Quotations.CountAsync());
        Assert.Equal(originalItemCount + 1, await db.Set<QuotationItem>().CountAsync());
        Assert.Equal(originalReceivableCount, await db.ReceivableTitles.CountAsync());
        Assert.Equal(originalWorkOrderCount, await db.WorkOrders.CountAsync());
    }
}
