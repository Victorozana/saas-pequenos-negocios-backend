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
        services.AddScoped<Agendamento.Api.Infrastructure.Tenancy.HttpTenantContext>();
        services.AddScoped<Agendamento.Api.Application.Tenancy.ITenantContext>(provider => provider.GetRequiredService<Agendamento.Api.Infrastructure.Tenancy.HttpTenantContext>());

        services.AddScoped<Agendamento.Api.Infrastructure.Notifications.ICommunicationGateway, Agendamento.Api.Infrastructure.Notifications.MockCommunicationGateway>();
        services.AddScoped<Agendamento.Api.Application.Notifications.INotificationService, Agendamento.Api.Infrastructure.Notifications.NotificationService>();
        services.AddHostedService<Agendamento.Api.Infrastructure.Notifications.NotificationOutboxProcessor>();
        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddSingleton<TimeProvider>(services, TimeProvider.System);
        services.AddScoped<Agendamento.Api.Infrastructure.Notifications.OverdueReceivableReminderProcessor>();
        services.AddHostedService<Agendamento.Api.Infrastructure.Notifications.OverdueReceivableReminderWorker>();

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
                options.IncludeErrorDetails = false;
                options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var userClaim = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                        var tenantClaim = context.Principal?.FindFirst("tenant_id")?.Value;
                        if (!Guid.TryParse(userClaim, out var userId) || !Guid.TryParse(tenantClaim, out var tenantId))
                        {
                            context.Fail("Sessão inválida.");
                            return;
                        }
                        var db = context.HttpContext.RequestServices.GetRequiredService<Agendamento.Api.Infrastructure.Persistence.AgendamentoDbContext>();
                        using var administrative = db.Administrative.Enter();
                        var active = await db.TenantMemberships.IgnoreQueryFilters()
                            .Where(m => m.TenantId == tenantId && m.UserId == userId && m.IsActive)
                            .Join(db.Users.Where(u => u.Status == Agendamento.Domain.Identity.UserStatus.Active), m => m.UserId, u => u.Id, (m, u) => m.Id)
                            .AnyAsync(context.HttpContext.RequestAborted);
                        if (!active) context.Fail("Acesso revogado.");
                    }
                };
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
        services.AddScoped<Agendamento.Api.Infrastructure.Persistence.AdministrativeDatabaseScope>();
        services.AddScoped<Agendamento.Api.Infrastructure.Persistence.TenantTransactionInterceptor>();
        services.AddScoped<Agendamento.Api.Infrastructure.Persistence.TenantConnectionInterceptor>();

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
                options.UseNpgsql(connectionString).AddInterceptors(interceptor, sp.GetRequiredService<Agendamento.Api.Infrastructure.Persistence.TenantTransactionInterceptor>(), sp.GetRequiredService<Agendamento.Api.Infrastructure.Persistence.TenantConnectionInterceptor>());
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
