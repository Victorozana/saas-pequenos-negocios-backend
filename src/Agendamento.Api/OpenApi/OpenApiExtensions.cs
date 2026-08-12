namespace Agendamento.Api.OpenApi;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;

public static class OpenApiExtensions
{
    public static IServiceCollection AddOpenApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options =>
        {
            options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0;
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Info.Title = "Agendamento API v1";
                return System.Threading.Tasks.Task.CompletedTask;
            });
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                var environment = context.ApplicationServices.GetRequiredService<IHostEnvironment>();
                if (!environment.IsDevelopment() && !environment.IsEnvironment("Test"))
                {
                    return System.Threading.Tasks.Task.CompletedTask;
                }

                if (!document.Paths.TryGetValue("/api/v1/tenants", out var tenantPath) ||
                    tenantPath is null ||
                    tenantPath.Operations is null ||
                    !tenantPath.Operations.TryGetValue(HttpMethod.Post, out var tenantRegistration) ||
                    tenantRegistration is null ||
                    tenantRegistration.Responses is null ||
                    !tenantRegistration.Responses.TryGetValue("201", out var createdResponse) ||
                    createdResponse is not OpenApiResponse createdResponseDocument)
                {
                    return System.Threading.Tasks.Task.CompletedTask;
                }

                createdResponseDocument.Headers ??= new Dictionary<string, IOpenApiHeader>();
                createdResponseDocument.Headers["X-Development-Verification-Token"] = new OpenApiHeader
                {
                    Description = "Development/Test only. Token used to confirm the administrator email after registration.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String },
                };

                return System.Threading.Tasks.Task.CompletedTask;
            });
            options.AddOperationTransformer<ProblemDetailsOperationTransformer>();
            
            // Prevent exposing verificationToken in the public RegisterTenantResponse schema (AC-038)
            options.AddSchemaTransformer((schema, context, cancellationToken) =>
            {
                if (context.JsonTypeInfo?.Type == typeof(Agendamento.Api.Features.Tenants.RegisterTenantResponse))
                {
                    schema.Properties?.Remove("verificationToken");
                }
                return System.Threading.Tasks.Task.CompletedTask;
            });
        });

        return services;
    }

    public static IApplicationBuilder UseOpenApiDocumentation(this WebApplication app)
    {
        app.MapOpenApi("/openapi/{documentName}.json");

        if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Test"))
        {
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/openapi/v1.json", "Agendamento API v1");
                options.RoutePrefix = "swagger";
            });
        }

        return app;
    }
}
