using System.Security.Cryptography;
using Agendamento.Application.Identity;
using Agendamento.Application.Identity.CreateSession;
using Agendamento.Application.Identity.VerifyEmail;
using Agendamento.Infrastructure.Email;
using Agendamento.Infrastructure.Identity;

namespace Agendamento.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<InMemoryIdentityStore>();
        services.AddSingleton<IIdentityAuthenticationStore>(provider => provider.GetRequiredService<InMemoryIdentityStore>());
        services.AddSingleton<IEmailVerificationTokenRepository>(provider => provider.GetRequiredService<InMemoryIdentityStore>());
        services.AddSingleton<IEmailVerificationUserRepository>(provider => provider.GetRequiredService<InMemoryIdentityStore>());
        services.AddSingleton<IEmailOutbox>(provider => provider.GetRequiredService<InMemoryIdentityStore>());
        services.AddSingleton<IPasswordService, AspNetPasswordService>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IEmailVerificationSender, OutboxEmailVerificationSender>();

        var signingKey = configuration["Authentication:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        }

        services.AddSingleton<ISessionIssuer>(new JwtSessionIssuer(JwtSessionIssuerOptions.Create(signingKey)));

        if (configuration.GetValue<bool>("Database:RequirePostgreSql"))
        {
            var connectionString = configuration.GetConnectionString("PostgreSql");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "PostgreSQL configuration is required. Set ConnectionStrings__PostgreSql.");
            }

            try
            {
                var connectionStringBuilder = new System.Data.Common.DbConnectionStringBuilder
                {
                    ConnectionString = connectionString,
                };

                if (!connectionStringBuilder.TryGetValue("Host", out var host) ||
                    string.IsNullOrWhiteSpace(host?.ToString()))
                {
                    throw new ArgumentException("The PostgreSQL connection string must contain a Host.");
                }
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException(
                    "PostgreSQL configuration is invalid. Check ConnectionStrings__PostgreSql.",
                    exception);
            }
        }

        return services;
    }
}
