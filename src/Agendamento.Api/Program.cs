using Agendamento.Application;
using Agendamento.Api.Features.Identity;
using Agendamento.Infrastructure;

using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Agendamento.Api.Features.Tenants;
using Agendamento.Api.Features.Customers;
using Agendamento.Api.Features.Services;
using Agendamento.Api.Features.Quotations;
using Agendamento.Api.Features.WorkOrders;
using Agendamento.Api.Features.Appointments;
using Agendamento.Api.Features.Financial;
using Agendamento.Api.Features.Notifications;
using Agendamento.Api.Features.Dashboard;
using Agendamento.Api.Features.Team;
using Agendamento.Api.OpenApi;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddOpenApiDocumentation();

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
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<Agendamento.Api.Infrastructure.Tenancy.TenantContextMiddleware>();
app.UseOpenApiDocumentation();

app.MapHealthChecks("/health")
   .ExcludeFromDescription()
   .WithName("HealthCheck")
   .WithTags("Health");
app.MapEmailVerificationEndpoints();
app.MapSessionEndpoints();
app.MapCompanyRegistryEndpoints();
app.MapTenantRegistrationEndpoints();
app.MapCurrentUserEndpoints();
app.MapCurrentTenantEndpoints();
app.MapCustomerEndpoints();
app.MapServiceEndpoints();
app.MapQuotationEndpoints();
app.MapWorkOrderEndpoints();
app.MapAppointmentEndpoints();
app.MapFinancialEndpoints();
app.MapNotificationEndpoints();
app.MapDashboardEndpoints();
app.MapTeamMemberEndpoints();
Agendamento.Api.Features.Subscriptions.SubscriptionEndpoints.MapSubscriptionEndpoints(app);

app.Run();

public partial class Program;
