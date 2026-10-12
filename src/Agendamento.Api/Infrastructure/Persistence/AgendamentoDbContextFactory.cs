using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Agendamento.Api.Infrastructure.Persistence;

public class AgendamentoDbContextFactory : IDesignTimeDbContextFactory<AgendamentoDbContext>
{
    public AgendamentoDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AgendamentoDbContext>();
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<AgendamentoDbContextFactory>()
            .AddEnvironmentVariables()
            .Build();
        var connectionString = configuration.GetConnectionString("PostgreSql")
            ?? throw new InvalidOperationException("Configure ConnectionStrings:PostgreSql nos segredos locais antes de aplicar migrations.");
        optionsBuilder.UseNpgsql(connectionString);

        return new AgendamentoDbContext(optionsBuilder.Options, new DummyTenantContext());
    }

    private sealed class DummyTenantContext : Agendamento.Api.Application.Tenancy.ITenantContext
    {
        public Guid TenantId => Guid.Empty;
        public bool HasTenant => false;
    }
}
