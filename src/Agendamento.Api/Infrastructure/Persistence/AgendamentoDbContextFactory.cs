using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Agendamento.Api.Infrastructure.Persistence;

public class AgendamentoDbContextFactory : IDesignTimeDbContextFactory<AgendamentoDbContext>
{
    public AgendamentoDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AgendamentoDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=agendamento;Username=postgres;Password=postgres");

        return new AgendamentoDbContext(optionsBuilder.Options);
    }
}
