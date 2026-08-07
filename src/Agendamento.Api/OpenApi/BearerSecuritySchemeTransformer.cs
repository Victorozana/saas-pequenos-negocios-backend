using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Agendamento.Api.OpenApi;

public class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var scheme = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Name = "Authorization",
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\""
        };

        if (document.Components == null)
        {
            document.Components = new OpenApiComponents();
        }

        document.Components.SecuritySchemes ??= new Dictionary<string, Microsoft.OpenApi.IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes.Add("Bearer", scheme);

        // We could also add security requirements here globally, but typically we want it per-operation 
        // if they require auth, or globally if everything requires it. 
        // Wait, the easiest way in .NET 9 is to use endpoints metadata `.RequireAuthorization()` which OpenAPI handles natively if we register the scheme.
        return Task.CompletedTask;
    }
}
