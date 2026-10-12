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
            options.AddOperationTransformer<ProblemDetailsOperationTransformer>();
            options.AddSchemaTransformer((schema, context, cancellationToken) =>
            {
                if (context.JsonTypeInfo.Type == typeof(Agendamento.Api.Application.Customers.GetCustomers.CustomerResponse) &&
                    schema.Properties?.TryGetValue("documentValue", out var property) == true && property is OpenApiSchema documentValue)
                {
                    documentValue.Pattern = Agendamento.Api.Application.Customers.GetCustomers.CustomerResponse.DocumentDisplayPattern;
                    documentValue.Description = "CPF mascarado (somente dois dígitos finais), CNPJ ou null. CPF integral nunca é retornado.";
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
