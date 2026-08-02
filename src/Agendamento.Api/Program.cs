using Agendamento.Application;
using Agendamento.Infrastructure;
using Agendamento.Api.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddHealthChecks();
builder.Services.AddOpenApiDocumentation();

var app = builder.Build();

app.UseOpenApiDocumentation();
app.MapHealthChecks("/health");
app.Run();

public partial class Program;
