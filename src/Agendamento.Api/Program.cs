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
builder.Services.Configure<Microsoft.AspNetCore.Routing.RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
        await Results.Problem(statusCode: 429, title: "Limite de consultas excedido. Tente novamente mais tarde.")
            .ExecuteAsync(context.HttpContext);
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

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    ExceptionHandler = context => Results.Problem(statusCode: 500,
        title: "Não foi possível concluir a operação. Tente novamente mais tarde.").ExecuteAsync(context),
    // Exceptions can contain credentials supplied to a downstream provider. Keep
    // both the HTTP response and default exception diagnostics free of that data.
    SuppressDiagnosticsCallback = _ => true
});
// Minimal API binding failures otherwise have an empty body. Only these input
// failures get ProblemDetails; intentional empty 401/404/204 remain untouched.
app.UseStatusCodePages(async context =>
{
    var status = context.HttpContext.Response.StatusCode;
    if (status is 400 or 415)
        await Results.Problem(statusCode: status, title: status == 400
            ? "Dados da requisição inválidos." : "Tipo de conteúdo não suportado.")
            .ExecuteAsync(context.HttpContext);
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<Agendamento.Api.Infrastructure.Tenancy.TenantContextMiddleware>();
app.UseOpenApiDocumentation();

app.MapHealthChecks("/health")
   .WithMetadata(
       // ApiExplorer requires the actual handler MethodInfo for middleware endpoints.
       typeof(Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckMiddleware).GetMethod("InvokeAsync")!,
       new Microsoft.AspNetCore.Routing.HttpMethodMetadata(["GET"]),
       new Microsoft.AspNetCore.Http.ProducesResponseTypeMetadata(StatusCodes.Status200OK, typeof(string), ["text/plain"]),
       new Microsoft.AspNetCore.Http.ProducesResponseTypeMetadata(StatusCodes.Status503ServiceUnavailable, typeof(string), ["text/plain"]))
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

await Agendamento.Api.Infrastructure.Persistence.DbSeeder.SeedDefaultUserAsync(app.Services);

app.Run();

public partial class Program;
