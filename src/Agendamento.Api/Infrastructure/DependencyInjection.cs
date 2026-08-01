namespace Agendamento.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Registre aqui EF Core/Npgsql, Redis, mensageria e implementações de ports.
        // As dependências serão adicionadas ao iniciar a implementação de cada módulo.
        return services;
    }
}
