using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Agendamento.Api.Infrastructure.Persistence;

namespace Agendamento.IntegrationTests.Infrastructure;

public sealed class AgendamentoApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddDbContext<AgendamentoDbContext>(options =>
            {
                options.UseInMemoryDatabase("IntegrationTestDb");
            });
            // Adicionar UnitOfWork se não estiver registrado
            services.AddScoped<Agendamento.Api.Application.Common.IUnitOfWork, UnitOfWork>();
        });
    }
}

