using Microsoft.OpenApi;

namespace Agendamento.Api.OpenApi;

public static class DownloadOperationExtensions
{
    public static RouteHandlerBuilder DocumentDownload(this RouteHandlerBuilder builder, string mediaType, bool binary)
        => builder.AddOpenApiOperationTransformer((operation, _, _) =>
        {
            var response = new OpenApiResponse { Description = operation.Responses!["200"].Description };
            operation.Responses["200"] = response;
            response.Content = new Dictionary<string, OpenApiMediaType>
            {
                [mediaType] = new() { Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = binary ? "binary" : null } }
            };
            response.Headers = new Dictionary<string, IOpenApiHeader>
            {
                ["Content-Disposition"] = new OpenApiHeader
                {
                    Description = "attachment; filename e filename* informam o nome do arquivo para download.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                },
                ["Content-Length"] = new OpenApiHeader
                {
                    Description = "Tamanho do arquivo em bytes.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int64" }
                }
            };
            return Task.CompletedTask;
        });
}
