using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Agendamento.Api.OpenApi;

public class ProblemDetailsOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        // Add ProblemDetails to 4xx and 5xx responses if not already documented
        operation.Responses ??= new OpenApiResponses();
        foreach (var response in operation.Responses)
        {
            if (response.Key.StartsWith("4") || response.Key.StartsWith("5"))
            {
                var rsp = response.Value;
                if (rsp != null && rsp.Content != null && !rsp.Content.ContainsKey("application/problem+json"))
                {
                    rsp.Content.Add("application/problem+json", new OpenApiMediaType
                    {
                        Schema = new OpenApiSchemaReference("ProblemDetails", context.Document)
                    });
                }
            }
        }
        
        return Task.CompletedTask;
    }
}
