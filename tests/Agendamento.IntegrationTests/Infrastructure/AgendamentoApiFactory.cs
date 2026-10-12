using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Agendamento.Api.Infrastructure.Persistence;

namespace Agendamento.IntegrationTests.Infrastructure;

public sealed class AgendamentoApiFactory : WebApplicationFactory<Program>
{
    public async Task<Guid> CreateActiveMemberAsync(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AgendamentoDbContext>().UseInMemoryDatabase("IntegrationTestDb").Options;
        using var db = new AgendamentoDbContext(options, new TestTenant(tenantId));
        var user = Agendamento.Domain.Identity.User.Create("Teste", null, $"{Guid.NewGuid():N}@example.test", "test-hash");
        user.ConfirmEmail(DateTimeOffset.UtcNow);
        db.Users.Add(user);
        db.TenantMemberships.Add(Agendamento.Domain.Tenants.TenantMembership.Create(tenantId, user.Id, "Admin", false));
        await db.SaveChangesAsync();
        return user.Id;
    }

    private sealed class TestTenant(Guid id) : Agendamento.Api.Application.Tenancy.ITenantContext
    {
        public Guid TenantId => id;
        public bool HasTenant => true;
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["Database:RequirePostgreSql"] = "false" }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AgendamentoDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AgendamentoDbContext>>();
            services.AddDbContext<AgendamentoDbContext>(options =>
            {
                options.UseInMemoryDatabase("IntegrationTestDb");
            });
            // Adicionar UnitOfWork se não estiver registrado
            services.AddScoped<Agendamento.Api.Application.Common.IUnitOfWork, UnitOfWork>();
        });
    }
}

