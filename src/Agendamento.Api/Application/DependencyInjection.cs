using Agendamento.Application.Identity.CreateSession;
using Agendamento.Application.Identity.VerifyEmail;

namespace Agendamento.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddTransient<VerifyEmailHandler>();
        services.AddTransient<CreateSessionHandler>();

        // Tenants
        services.AddTransient<Agendamento.Api.Application.Tenants.RegisterTenant.RegisterTenantHandler>();

        // Customers
        services.AddTransient<Agendamento.Api.Application.Customers.CreateCustomer.CreateCustomerHandler>();
        services.AddTransient<Agendamento.Api.Application.Customers.UpdateCustomer.UpdateCustomerHandler>();
        services.AddTransient<Agendamento.Api.Application.Customers.GetCustomers.GetCustomersHandler>();
        services.AddTransient<Agendamento.Api.Application.Customers.GetCustomers.GetCustomerByIdHandler>();
        
        return services;
    }
}
