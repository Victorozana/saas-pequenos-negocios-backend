namespace Agendamento.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Registre aqui casos de uso, validadores e handlers de eventos de domínio.
        return services;
    }
}
