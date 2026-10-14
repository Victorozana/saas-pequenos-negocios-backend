using System.Net;
using System.Net.Http.Json;
using Agendamento.Api.Application.Services.CreateService;
using Agendamento.Api.Application.Services.UpdateService;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Services;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.IntegrationTests.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;
using static Agendamento.IntegrationTests.Tenancy.PostgreSqlRlsTests;

namespace Agendamento.IntegrationTests.Services;

public sealed class ServiceCatalogValidationTests : IClassFixture<TeamFinancialTestFactory>
{
    private readonly TeamFinancialTestFactory factory;
    public ServiceCatalogValidationTests(TeamFinancialTestFactory factory) => this.factory = factory;
    private static readonly InMemoryDatabaseRoot Root = new();
    private sealed class Tenant(Guid id) : ITenantContext { public Guid TenantId => id; public bool HasTenant => true; }
    private sealed class Saves : SaveChangesInterceptor
    {
        public int Count { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        { Count++; return ValueTask.FromResult(result); }
    }

    [Theory(DisplayName = "Handlers recusam catálogo excedido sem mutação rastreada ou SaveChanges @spec:AC-045")]
    [InlineData(201, 1, 1)]
    [InlineData(1, 1001, 1)]
    [InlineData(1, 1, 51)]
    public async Task HandlersRejectWithoutSaving(int nameLength, int descriptionLength, int unitLength)
    {
        var tenant = new Tenant(Guid.NewGuid());
        var saves = new Saves();
        var options = new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), Root).AddInterceptors(new TenantSaveChangesInterceptor(tenant), saves).Options;
        using var db = new AgendamentoDbContext(options, tenant);
        var original = ServiceItem.Create(tenant.TenantId, "Original", null, "un", 0.01m);
        db.ServiceItems.Add(original);
        await db.SaveChangesAsync();
        var queued = ServiceItem.Create(tenant.TenantId, "Enfileirado", null, "un", 0m);
        db.ServiceItems.Add(queued);
        var before = saves.Count;
        var name = new string('n', nameLength);
        var description = new string('d', descriptionLength);
        var unit = new string('u', unitLength);
        await Assert.ThrowsAsync<ArgumentException>(() => new CreateServiceHandler(db, tenant).HandleAsync(new(name, description, unit, 2m)));
        await Assert.ThrowsAsync<ArgumentException>(() => new UpdateServiceHandler(db).HandleAsync(original.Id, new(name, description, unit, 2m, false)));
        db.ChangeTracker.DetectChanges();
        Assert.Equal(before, saves.Count);
        Assert.Equal(EntityState.Unchanged, db.Entry(original).State);
        Assert.Equal(EntityState.Added, db.Entry(queued).State);
        Assert.Equal(2, db.ChangeTracker.Entries<ServiceItem>().Count());
        using var fresh = new AgendamentoDbContext(options, tenant);
        var persisted = await fresh.ServiceItems.SingleAsync();
        Assert.Equal("Original", persisted.Name);
        Assert.Null(persisted.Description);
        Assert.Equal("un", persisted.Unit);
        Assert.Equal(0.01m, persisted.BasePrice);
        Assert.True(persisted.IsActive);
        Assert.Equal(1, await db.SaveChangesAsync());
        Assert.Equal(before + 1, saves.Count);
        using var afterSave = new AgendamentoDbContext(options, tenant);
        Assert.Equal(2, await afterSave.ServiceItems.CountAsync());
        var preserved = await afterSave.ServiceItems.SingleAsync(s => s.Id == original.Id);
        Assert.Equal(tenant.TenantId, preserved.TenantId);
        Assert.Equal("Original", preserved.Name);
        Assert.Null(preserved.Description);
        Assert.Equal("un", preserved.Unit);
        Assert.Equal(0.01m, preserved.BasePrice);
        Assert.True(preserved.IsActive);
        var savedQueued = await afterSave.ServiceItems.SingleAsync(s => s.Id == queued.Id);
        Assert.Equal(tenant.TenantId, savedQueued.TenantId);
        Assert.Equal("Enfileirado", savedQueued.Name);
        Assert.Null(savedQueued.Description);
        Assert.Equal("un", savedQueued.Unit);
        Assert.Equal(0m, savedQueued.BasePrice);
        Assert.True(savedQueued.IsActive);
    }

    [Fact(DisplayName = "HTTP catálogo aceita limites e recusa excesso preservando persistência em request novo @spec:AC-045")]
    public Task HttpBoundariesAndRejection() => ProveHttpBoundariesAndRejection(factory);

    [Fact(DisplayName = "HTTP catálogo aceita limites e recusa excesso em PostgreSQL temporário restrito @spec:AC-045")]
    public async Task PostgreSqlHttpBoundariesAndRejection()
    {
        await using var database = await Agendamento.IntegrationTests.Tenancy.PostgreSqlRlsTests.Database.Create(seed: false);
        using (var setup = new AgendamentoDbContext(new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseNpgsql(database.MigrationConnection).Options, new Tenant(Guid.Empty)))
        {
            var tenant = Agendamento.Domain.Tenants.Tenant.Create(
                Agendamento.Domain.Tenants.Cnpj.Create("11222333000181"), "Catálogo temporário", "Catálogo", "LTDA", "5611201",
                Agendamento.Domain.Tenants.BusinessCategory.PrestadorServicoAutonomo, "catalogo@example.test", "11999999999",
                Agendamento.Domain.Tenants.TenantAddress.Create("Rua", "1", "", "Centro", "São Paulo", "SP", "01000000"));
            setup.Tenants.Add(tenant);
            setup.Entry(tenant).Property(t => t.Id).CurrentValue = database.A;
            await setup.SaveChangesAsync();
        }
        using var postgresFactory = new TeamFinancialTestFactory(database.RestrictedConnection, database.AdministrativeConnection);
        await ProveHttpBoundariesAndRejection(postgresFactory, database.A);
    }

    private static async Task ProveHttpBoundariesAndRejection(TeamFinancialTestFactory factory, Guid? tenantId = null)
    {
        var (client, _, tenant) = await factory.Member("owner_admin", [], tenantId);
        using (client)
        {
            var valid = new CreateServiceCommand(new string('n', 200), new string('d', 1000), new string('u', 50), 9999999999999999.99m);
            var response = await client.PostAsJsonAsync("/api/v1/services", valid);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var id = (await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
            Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/v1/services/{id}", new UpdateServiceCommand(valid.Name, null, valid.Unit, 0m, true))).StatusCode);
            var snapshot = await client.GetStringAsync($"/api/v1/services/{id}");
            foreach (var lengths in new[] { (201, 1, 1), (1, 1001, 1), (1, 1, 51) })
            {
                var name = new string('n', lengths.Item1);
                var description = new string('d', lengths.Item2);
                var unit = new string('u', lengths.Item3);
                using var rejectedCreate = await client.PostAsJsonAsync("/api/v1/services", new CreateServiceCommand(name, description, unit, 1m));
                using var rejectedUpdate = await client.PutAsJsonAsync($"/api/v1/services/{id}", new UpdateServiceCommand(name, description, unit, 1m, false));
                foreach (var rejected in new[] { rejectedCreate, rejectedUpdate })
                {
                    Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
                    Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
                    var problem = await rejected.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                    Assert.Equal(400, problem.GetProperty("status").GetInt32());
                    Assert.Equal("Informe nome, unidade e preço não negativo para o serviço.", problem.GetProperty("title").GetString());
                    Assert.False(problem.TryGetProperty("exception", out _));
                    Assert.False(problem.TryGetProperty("stackTrace", out _));
                    Assert.False(problem.TryGetProperty("detail", out _));
                }
                Assert.Equal(snapshot, await client.GetStringAsync($"/api/v1/services/{id}"));
                using var fresh = factory.Db(tenant);
                var saved = await fresh.ServiceItems.SingleAsync();
                Assert.Equal(id, saved.Id);
                Assert.Null(saved.Description);
                Assert.Equal(0m, saved.BasePrice);
                Assert.True(saved.IsActive);
            }
        }
    }
}

