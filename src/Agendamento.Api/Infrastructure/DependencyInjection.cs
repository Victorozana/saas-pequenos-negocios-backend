using System.Security.Cryptography;
using Agendamento.Application.Identity;
using Agendamento.Application.Identity.CreateSession;
using Agendamento.Application.Identity.VerifyEmail;
using Agendamento.Infrastructure.Email;
using Agendamento.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

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

        services.AddHttpContextAccessor();
        services.AddScoped<Agendamento.Api.Application.Tenancy.ITenantContext, Agendamento.Api.Infrastructure.Tenancy.HttpTenantContext>();

        var signingKey = configuration["Authentication:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        }

        services.AddSingleton<ISessionIssuer>(new JwtSessionIssuer(JwtSessionIssuerOptions.Create(signingKey)));

        services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(signingKey)),
                    ValidateIssuer = false,
                    ValidateAudience = false
                };
            });
        services.AddAuthorization();

        if (configuration.GetValue<bool>("Database:RequirePostgreSql"))
        {
            var connectionString = configuration.GetConnectionString("PostgreSql");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "PostgreSQL configuration is required. Set ConnectionStrings__PostgreSql.");
            }

            services.AddScoped<Agendamento.Api.Infrastructure.Persistence.TenantSaveChangesInterceptor>();

            services.AddDbContext<Agendamento.Api.Infrastructure.Persistence.AgendamentoDbContext>((sp, options) =>
            {
                var interceptor = sp.GetRequiredService<Agendamento.Api.Infrastructure.Persistence.TenantSaveChangesInterceptor>();
                options.UseNpgsql(connectionString).AddInterceptors(interceptor);
            });

            services.AddScoped<Agendamento.Api.Application.Common.IUnitOfWork, Agendamento.Api.Infrastructure.Persistence.UnitOfWork>();
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
