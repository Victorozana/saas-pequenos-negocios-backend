using Agendamento.Application;
using Agendamento.Api.Features.Identity;
using Agendamento.Infrastructure;

using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Agendamento.Api.Features.Tenants;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddHealthChecks();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("CompanyRegistryLimit", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1)
            }));
});

var app = builder.Build();

app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapEmailVerificationEndpoints();
app.MapSessionEndpoints();
app.MapCompanyRegistryEndpoints();
app.MapTenantRegistrationEndpoints();
app.MapCurrentUserEndpoints();
app.MapCurrentTenantEndpoints();

app.Run();

public partial class Program;
