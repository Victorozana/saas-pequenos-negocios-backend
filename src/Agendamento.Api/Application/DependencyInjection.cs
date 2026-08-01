using Agendamento.Application.Identity.CreateSession;
using Agendamento.Application.Identity.VerifyEmail;

namespace Agendamento.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddTransient<VerifyEmailHandler>();
        services.AddTransient<CreateSessionHandler>();
        return services;
    }
}
