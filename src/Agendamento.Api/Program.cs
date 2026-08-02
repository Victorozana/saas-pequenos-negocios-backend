using Agendamento.Application;
using Agendamento.Api.Features.Identity;
using Agendamento.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapEmailVerificationEndpoints();
app.MapSessionEndpoints();
app.Run();

public partial class Program;
