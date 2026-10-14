using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Agendamento.Api.Application.Quotations.CreateQuotation;
using Agendamento.Api.Application.Quotations.UpdateQuotation;
using Agendamento.Api.Application.Quotations.UpdateQuotationStatus;
using Agendamento.Api.Domain.Customers;
using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Domain.WorkOrders;
using Agendamento.Api.Features.Common;
using Agendamento.IntegrationTests.Features;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using static Agendamento.IntegrationTests.Tenancy.PostgreSqlRlsTests;

namespace Agendamento.IntegrationTests.Quotations;

public sealed class QuotationConversionConcurrencyTests
{
    private static string Route(Guid id, bool alternative) => alternative
        ? $"/api/v1/work-orders/from-quotation/{id}" : $"/api/v1/quotations/{id}/convert";

    [Fact(DisplayName = "Conversões nas duas rotas aguardam aprovação inicial e preservam seus recebíveis @spec:AC-091 @spec:AC-093")]
    public async Task InitialApprovalCommitsBeforeWaitingConversions()
    {
        await using var database = await Database.Create();
        using var factory = new TeamFinancialTestFactory(database.RestrictedConnection, database.AdministrativeConnection);
        var (client, _, tenant) = await factory.Member("owner_admin", [], database.A);
        var (id, _) = await Seed(factory, client, tenant, approve: false);
        using var approvingDb = factory.Db(tenant);
        await using var transaction = await approvingDb.Database.BeginTransactionAsync();
        await approvingDb.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Quotations\" WHERE \"Id\"={id} FOR UPDATE");
        var quote = await approvingDb.Quotations.Include(q => q.Items).SingleAsync(q => q.Id == id);
        quote.Approve();
        await new Agendamento.Api.Application.Financial.GenerateReceivablesFromQuotation.GenerateReceivablesFromQuotationHandler(approvingDb).HandleAsync(quote);
        await approvingDb.SaveChangesAsync();
        var expected = JsonSerializer.Serialize(await approvingDb.ReceivableTitles.AsNoTracking().Where(r => r.QuotationId == id).OrderBy(r => r.Id)
            .Select(r => new { r.Id, r.TenantId, r.CustomerId, r.QuotationId, r.Description, r.OriginalAmount, r.DueDate, r.Status }).ToListAsync());
        var first = client.PostAsync(Route(id, false), null);
        var second = client.PostAsync(Route(id, true), null);
        try { await WaitForLocks(database, 2); }
        finally { await transaction.CommitAsync(); }
        var orderId = await Converted(await first, false);
        Assert.Equal(orderId, await Converted(await second, true));
        await AssertPersisted(factory, tenant, id, orderId, expected);
    }

    [Fact(DisplayName = "Conversões aguardam edição Pending confirmada e recusam estado atualizado sem mutação @spec:AC-091 @spec:AC-206")]
    public async Task PendingEditCommitsBeforeRefusedConversions()
    {
        await using var database = await Database.Create();
        using var factory = new TeamFinancialTestFactory(database.RestrictedConnection, database.AdministrativeConnection);
        var (client, _, tenant) = await factory.Member("owner_admin", [], database.A);
        var (id, command) = await Seed(factory, client, tenant, approve: false);
        using var editingDb = factory.Db(tenant);
        await using var transaction = await editingDb.Database.BeginTransactionAsync();
        await editingDb.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Quotations\" WHERE \"Id\"={id} FOR UPDATE");
        var quote = await editingDb.Quotations.Include(q => q.Items).SingleAsync(q => q.Id == id);
        var previousItems = quote.Items.ToList();
        var replacements = new[] { QuotationItem.Create(null, "Conteúdo atualizado", "un", 15m, 2m, 1m) };
        quote.UpdatePendingContent(command.Code, command.CustomerId, command.IssueDate, null, "Edição confirmada", "Novos termos",
            replacements, DepositType.FixedAmount, 5m, "Entrada editada");
        editingDb.RemoveRange(previousItems);
        editingDb.AddRange(replacements);
        await editingDb.SaveChangesAsync();
        var first = client.PostAsync(Route(id, false), null);
        var second = client.PostAsync(Route(id, true), null);
        try { await WaitForLocks(database, 2); }
        finally { await transaction.CommitAsync(); }
        Assert.Equal(HttpStatusCode.BadRequest, (await first).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await second).StatusCode);
        using var savedDb = factory.Db(tenant);
        var saved = await savedDb.Quotations.Include(q => q.Items).SingleAsync(q => q.Id == id);
        Assert.Equal(QuotationStatus.Pending, saved.Status); Assert.Equal("Edição confirmada", saved.Notes);
        Assert.Equal("Novos termos", saved.PaymentTerms); Assert.Equal(29m, saved.TotalAmount);
        Assert.Equal(5m, saved.DepositInfo!.RequiredAmount); Assert.Equal(24m, saved.DepositInfo.RemainingBalance);
        Assert.Equal("Conteúdo atualizado", Assert.Single(saved.Items).ServiceName);
        Assert.False(await savedDb.WorkOrders.AnyAsync(w => w.QuotationId == id));
        Assert.False(await savedDb.ReceivableTitles.AnyAsync(r => r.QuotationId == id));
    }

    [Theory(DisplayName = "Conversões HTTP realmente concorrentes convergem em uma OS sem duplicar itens ou recebíveis @spec:AC-091 @spec:AC-093")]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task SimultaneousConversions(bool firstAlternative, bool secondAlternative)
    {
        await using var database = await Database.Create();
        Assert.True(new NpgsqlConnectionStringBuilder(database.RestrictedConnection).MaxPoolSize >= 3);
        using var factory = new TeamFinancialTestFactory(database.RestrictedConnection, database.AdministrativeConnection);
        var (client, _, tenant) = await factory.Member("owner_admin", [], database.A);
        var (id, _) = await Seed(factory, client, tenant);
        var receivables = await Receivables(factory, tenant, id);
        await using var blocker = await database.Admin.BeginTransactionAsync();
        await Execute(database.Admin, "SELECT 1 FROM \"Quotations\" WHERE \"Id\"=@id::uuid FOR UPDATE", id.ToString());
        var first = client.PostAsync(Route(id, firstAlternative), null);
        var second = client.PostAsync(Route(id, secondAlternative), null);
        try { await WaitForLocks(database, 2); }
        finally { await blocker.CommitAsync(); }
        var firstId = await Converted(await first, firstAlternative);
        Assert.Equal(firstId, await Converted(await second, secondAlternative));
        Assert.Equal(firstId, await Converted(await client.PostAsync(Route(id, !firstAlternative), null), !firstAlternative));
        await AssertPersisted(factory, tenant, id, firstId, receivables);
    }

    [Theory(DisplayName = "Conversão coordena aprovação repetida, edição e decisões concorrentes pelo mesmo lock @spec:AC-091 @spec:AC-093 @spec:AC-206")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentInteractions(bool alternative)
    {
        await using var database = await Database.Create();
        using var factory = new TeamFinancialTestFactory(database.RestrictedConnection, database.AdministrativeConnection);
        var (client, _, tenant) = await factory.Member("owner_admin", [], database.A);
        var (id, command) = await Seed(factory, client, tenant);
        var receivables = await Receivables(factory, tenant, id);
        await using var blocker = await database.Admin.BeginTransactionAsync();
        await Execute(database.Admin, "SELECT 1 FROM \"Quotations\" WHERE \"Id\"=@id::uuid FOR UPDATE", id.ToString());
        var conversion = client.PostAsync(Route(id, alternative), null);
        var approval = client.PatchAsJsonAsync($"/api/v1/quotations/{id}/status", new UpdateQuotationStatusCommand(QuotationStatus.Approved));
        var rejection = client.PatchAsJsonAsync($"/api/v1/quotations/{id}/status", new UpdateQuotationStatusCommand(QuotationStatus.Rejected));
        var cancellation = client.PatchAsJsonAsync($"/api/v1/quotations/{id}/status", new UpdateQuotationStatusCommand(QuotationStatus.Canceled));
        var edit = client.PutAsJsonAsync($"/api/v1/quotations/{id}", new UpdateQuotationCommand(command.Code, command.CustomerId,
            command.IssueDate, null, "Stale edit", "Changed", [new(null, "Changed", "un", 999, 1, 0)], null));
        try { await WaitForLocks(database, 5); }
        finally { await blocker.CommitAsync(); }
        var orderId = await Converted(await conversion, alternative);
        Assert.Equal(HttpStatusCode.NoContent, (await approval).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await rejection).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await cancellation).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await edit).StatusCode);
        await AssertPersisted(factory, tenant, id, orderId, receivables);
    }

    [Theory(DisplayName = "Falha relacional controlada após escrita reverte conversão inteira nas duas rotas HTTP @spec:AC-091 @spec:AC-093")]
    [InlineData(false, "WorkOrders", "INSERT")]
    [InlineData(true, "WorkOrders", "INSERT")]
    [InlineData(false, "WorkOrderItems", "INSERT")]
    [InlineData(true, "WorkOrderItems", "INSERT")]
    [InlineData(false, "Quotations", "UPDATE")]
    [InlineData(true, "Quotations", "UPDATE")]
    public async Task RelationalFailureRollsBack(bool alternative, string table, string operation)
    {
        await using var database = await Database.Create();
        using var factory = new TeamFinancialTestFactory(database.RestrictedConnection, database.AdministrativeConnection);
        var (client, _, tenant) = await factory.Member("owner_admin", [], database.A);
        var (id, _) = await Seed(factory, client, tenant);
        var snapshot = await client.GetStringAsync($"/api/v1/quotations/{id}");
        var receivables = await Receivables(factory, tenant, id);
        // The sequence is deliberately nontransactional: its advancement proves that
        // the AFTER trigger ran after a relational write, even though that write rolls back.
        await Execute(database.Admin, "CREATE SEQUENCE t091_write_probe; GRANT USAGE ON SEQUENCE t091_write_probe TO agendamento_app_user;");
        await Execute(database.Admin, """
            CREATE FUNCTION t091_refuse_write() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN PERFORM nextval('t091_write_probe');
              RAISE EXCEPTION 't091_private_failure_marker' USING ERRCODE='P0001';
            END $$;
            """);
        await Execute(database.Admin, $"CREATE TRIGGER t091_refuse AFTER {operation} ON \"{table}\" FOR EACH ROW EXECUTE FUNCTION t091_refuse_write()");
        var response = await client.PostAsync(Route(id, alternative), null);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var problem = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("t091_private_failure_marker", problem);
        Assert.DoesNotContain("Postgres", problem, StringComparison.OrdinalIgnoreCase);
        Assert.True((bool)(await Scalar(database.Admin, "SELECT is_called FROM t091_write_probe"))!);
        Assert.Equal(snapshot, await client.GetStringAsync($"/api/v1/quotations/{id}"));
        Assert.Equal(receivables, await Receivables(factory, tenant, id));
        Assert.Equal(0L, await Scalar(database.Admin, "SELECT count(*) FROM \"WorkOrders\" WHERE \"QuotationId\"=@id::uuid", id.ToString()));
        Assert.Equal(0L, await Scalar(database.Admin, "SELECT count(*) FROM \"WorkOrderItems\" i LEFT JOIN \"WorkOrders\" w ON w.\"Id\"=i.\"WorkOrderId\" WHERE w.\"QuotationId\"=@id::uuid OR w.\"Id\" IS NULL", id.ToString()));
        await Execute(database.Admin, $"DROP TRIGGER t091_refuse ON \"{table}\"");
        var orderId = await Converted(await client.PostAsync(Route(id, alternative), null), alternative);
        await AssertPersisted(factory, tenant, id, orderId, receivables);
    }

    private static async Task WaitForLocks(Database database, int expected)
    {
        var samples = new List<object>();
        var observed = false;
        try
        {
            for (var attempt = 0; attempt < 200; attempt++)
            {
                // PostgreSQL retains its statistics snapshot within the observer's
                // open transaction. Refresh it before every concurrency observation.
                await Execute(database.Admin, "SELECT pg_stat_clear_snapshot()");
                var pids = (string)(await Scalar(database.Admin, "SELECT coalesce(string_agg(DISTINCT pid::text, ','), '') FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock' AND query LIKE '%Quotations%FOR UPDATE%'"))!;
                var distinctSessions = pids.Split(',', StringSplitOptions.RemoveEmptyEntries).Length;
                samples.Add(new { atUtc = DateTime.UtcNow, expected, distinctSessions, pids });
                if (distinctSessions >= expected) { observed = true; break; }
                await Task.Delay(50);
            }
        }
        finally
        {
            var repository = new DirectoryInfo(AppContext.BaseDirectory);
            while (!Directory.Exists(Path.Combine(repository.FullName, ".spec")) && repository.Parent != null)
                repository = repository.Parent;
            var directory = Path.Combine(repository.FullName, ".local-verification", "t091", "concurrency");
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, $"locks-{Guid.NewGuid():N}.json"), JsonSerializer.Serialize(new { expected, observed, samples }));
        }
        Assert.True(observed, $"Expected {expected} distinct PostgreSQL sessions simultaneously waiting for the quotation row; pool serialization is not concurrency evidence.");
    }

    private static async Task<(Guid Id, CreateQuotationCommand Command)> Seed(TeamFinancialTestFactory factory, HttpClient client, Guid tenant, bool approve = true)
    {
        var customer = Customer.Create(tenant, "Cliente conversão fictício", "123", null, null, null);
        using (var db = factory.Db(tenant)) { db.Customers.Add(customer); await db.SaveChangesAsync(); }
        var command = new CreateQuotationCommand($"CV-{Guid.NewGuid():N}", customer.Id, DateTime.UtcNow.Date, null,
            "Notas copiadas", "Termos", [new(null, "Fracionado", "hora", 10.01m, 1.25m, 0.51m), new(null, "Segundo", "un", 30m, 2m, 5m)],
            new(DepositType.Percentage, 25m, "Entrada"));
        var creation = await client.PostAsJsonAsync("/api/v1/quotations", command);
        Assert.Equal(HttpStatusCode.Created, creation.StatusCode);
        var id = (await creation.Content.ReadFromJsonAsync<CreatedIdResponse>())!.Id;
        if (approve) Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsJsonAsync($"/api/v1/quotations/{id}/status", new UpdateQuotationStatusCommand(QuotationStatus.Approved))).StatusCode);
        return (id, command);
    }

    private static async Task<Guid> Converted(HttpResponseMessage response, bool alternative)
    {
        Assert.Equal(alternative ? HttpStatusCode.Created : HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty(alternative ? "id" : "workOrderId").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        if (alternative) Assert.Equal($"/api/v1/work-orders/{id}", response.Headers.Location!.OriginalString);
        return id;
    }

    private static async Task<string> Receivables(TeamFinancialTestFactory factory, Guid tenant, Guid id)
    {
        using var db = factory.Db(tenant);
        return JsonSerializer.Serialize(await db.ReceivableTitles.AsNoTracking().Where(r => r.QuotationId == id).OrderBy(r => r.Id)
            .Select(r => new { r.Id, r.TenantId, r.CustomerId, r.QuotationId, r.Description, r.OriginalAmount, r.DueDate, r.Status }).ToListAsync());
    }

    private static async Task AssertPersisted(TeamFinancialTestFactory factory, Guid tenant, Guid quotationId, Guid orderId, string receivables)
    {
        using var db = factory.Db(tenant);
        var quote = await db.Quotations.Include(q => q.Items).SingleAsync(q => q.Id == quotationId);
        var order = Assert.Single(await db.WorkOrders.Include(w => w.Items).Where(w => w.QuotationId == quotationId).ToListAsync());
        Assert.Equal(orderId, order.Id); Assert.Equal(tenant, order.TenantId);
        Assert.Equal(quote.CustomerId, order.CustomerId); Assert.Equal(quotationId, order.QuotationId);
        Assert.Equal("OS-" + quote.Code, order.Code); Assert.Equal(quote.Notes, order.Notes);
        Assert.Equal(QuotationStatus.Converted, quote.Status); Assert.Equal(WorkOrderStatus.Scheduled, order.Status);
        Assert.Null(order.CompletedAt); Assert.Equal(DateTimeKind.Utc, order.CreatedAt.Kind);
        Assert.Equal(quote.Items.Count, order.Items.Count);
        foreach (var source in quote.Items)
        {
            var item = Assert.Single(order.Items, i => i.ServiceName == source.ServiceName);
            Assert.Equal(order.Id, item.WorkOrderId); Assert.NotEqual(source.Id, item.Id);
            Assert.Equal(source.ServiceItemId, item.ServiceItemId); Assert.Equal(source.Unit, item.Unit);
            Assert.Equal(source.Quantity, item.Quantity); Assert.Equal(source.UnitPrice, item.UnitPrice);
            Assert.Equal(source.DiscountAmount, item.DiscountAmount);
        }
        Assert.Equal(quote.TotalAmount, order.Items.Sum(i => Math.Round(i.Quantity * i.UnitPrice, 2, MidpointRounding.AwayFromZero) - i.DiscountAmount));
        Assert.Equal(receivables, await Receivables(factory, tenant, quotationId));
        using var other = factory.Db(Guid.NewGuid());
        Assert.False(await other.WorkOrders.AnyAsync(w => w.Id == orderId));
    }
}
