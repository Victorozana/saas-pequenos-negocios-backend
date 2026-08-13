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
        services.AddSingleton<IEmailOutbox>(provider => provider.GetRequiredService<InMemoryIdentityStore>());
        services.AddScoped<EfIdentityStore>();
        services.AddScoped<IIdentityAuthenticationStore>(provider => provider.GetRequiredService<EfIdentityStore>());
        services.AddScoped<IEmailVerificationTokenRepository>(provider => provider.GetRequiredService<EfIdentityStore>());
        services.AddScoped<IEmailVerificationUserRepository>(provider => provider.GetRequiredService<EfIdentityStore>());
        services.AddSingleton<IPasswordService, AspNetPasswordService>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IEmailVerificationSender, OutboxEmailVerificationSender>();

        services.AddHttpContextAccessor();
        services.AddScoped<Agendamento.Api.Application.Tenancy.ITenantContext, Agendamento.Api.Infrastructure.Tenancy.HttpTenantContext>();

        services.AddScoped<Agendamento.Api.Infrastructure.Notifications.ICommunicationGateway, Agendamento.Api.Infrastructure.Notifications.MockCommunicationGateway>();
        services.AddHostedService<Agendamento.Api.Infrastructure.Notifications.NotificationOutboxProcessor>();

        services.AddScoped<Agendamento.Api.Application.Subscriptions.Services.IPlanLimitsChecker, Agendamento.Api.Infrastructure.Subscriptions.PlanLimitsChecker>();

        services.AddHttpClient<
            Agendamento.Api.Application.Tenants.CompanyRegistry.ICompanyRegistryGateway,
            Agendamento.Api.Infrastructure.CompanyRegistry.CompanyRegistryGateway>(client =>
        {
            var baseUrl = configuration["CompanyRegistry:BaseUrl"] ?? "https://brasilapi.com.br/";
            client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(configuration.GetValue("CompanyRegistry:TimeoutSeconds", 10));
        });

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

        services.AddScoped<Agendamento.Api.Infrastructure.Persistence.TenantSaveChangesInterceptor>();

        if (configuration.GetValue<bool>("Database:RequirePostgreSql"))
        {
            var connectionString = configuration.GetConnectionString("PostgreSql");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "PostgreSQL configuration is required. Set ConnectionStrings__PostgreSql.");
            }

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
        else
        {
            services.AddDbContext<Agendamento.Api.Infrastructure.Persistence.AgendamentoDbContext>((sp, options) =>
            {
                var interceptor = sp.GetRequiredService<Agendamento.Api.Infrastructure.Persistence.TenantSaveChangesInterceptor>();
                options.UseInMemoryDatabase("AgendamentoDb").AddInterceptors(interceptor);
            });

            services.AddScoped<Agendamento.Api.Application.Common.IUnitOfWork, Agendamento.Api.Infrastructure.Persistence.UnitOfWork>();
        }

        return services;
    }
}
