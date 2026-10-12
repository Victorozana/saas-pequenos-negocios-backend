using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Microsoft.AspNetCore.Authorization;

namespace Agendamento.Api.OpenApi;

public class ProblemDetailsOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any())
        {
            operation.Security = new List<OpenApiSecurityRequirement>
            {
                new() { [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = new List<string>() }
            };
        }
        // Authentication challenges have no body. Metadata must describe the HTTP
        // response; a document transformer never writes a runtime ProblemDetails.
        operation.Responses ??= new OpenApiResponses();
        AddInputProblem("500", "Falha inesperada: resposta ProblemDetails segura, sem detalhes internos.");
        if (operation.RequestBody != null || operation.Parameters?.Any(p => p.In is ParameterLocation.Query or ParameterLocation.Header) == true)
        {
            AddInputProblem("400", "Dados da requisição inválidos.");
        }
        if (operation.RequestBody != null)
        {
            AddInputProblem("415", "Tipo de conteúdo não suportado.");
        }
        void AddInputProblem(string status, string description)
        {
            if (operation.Responses.ContainsKey(status)) return;
            operation.Responses[status] = new OpenApiResponse
            {
                Description = description,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/problem+json"] = new()
                    {
                        Schema = new OpenApiSchemaReference("ProblemDetails", context.Document)
                    }
                }
            };
        }
        if (metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any())
        {
            operation.Responses["401"] = new OpenApiResponse
            {
                Description = "Sessão ausente, inválida ou revogada. Resposta sem corpo.",
                Headers = new Dictionary<string, IOpenApiHeader>
                {
                    ["WWW-Authenticate"] = new OpenApiHeader
                    {
                        Description = "Desafio de autenticação Bearer.",
                        Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                    }
                }
            };
        }
        
        return Task.CompletedTask;
    }
}
