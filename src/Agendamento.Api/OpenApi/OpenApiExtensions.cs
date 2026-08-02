namespace Agendamento.Api.OpenApi;

public static class OpenApiExtensions
{
    private static readonly string OpenApiDocument = """
{
  "openapi": "3.0.1",
  "info": {
    "title": "Agendamento API",
    "version": "v1",
    "description": "Contrato OpenAPI da API de agendamento."
  },
  "paths": {
    "/health": {
      "get": {
        "tags": [
          "Health"
        ],
        "operationId": "GetHealth",
        "responses": {
          "200": {
            "description": "Success"
          }
        }
      }
    }
  }
}
""";

    public static IServiceCollection AddOpenApiDocumentation(this IServiceCollection services)
    {
        return services;
    }

    public static WebApplication UseOpenApiDocumentation(this WebApplication app)
    {
        app.MapGet("/openapi/v1.json", () => Results.Text(OpenApiDocument, "application/json"))
            .ExcludeFromDescription();

        if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
        {
            app.MapGet("/swagger", () =>
            {
                const string html = """
<!doctype html>
<html lang="pt-BR">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Agendamento API Swagger</title>
  <link rel="stylesheet" href="https://unpkg.com/swagger-ui-dist@5/swagger-ui.css" />
  <style>
    body { margin: 0; background: #f6f7fb; }
    #swagger-ui { max-width: 1440px; margin: 0 auto; }
  </style>
</head>
<body>
  <div id="swagger-ui"></div>
  <script src="https://unpkg.com/swagger-ui-dist@5/swagger-ui-bundle.js"></script>
  <script>
    window.ui = SwaggerUIBundle({
      url: '/openapi/v1.json',
      dom_id: '#swagger-ui',
      deepLinking: true,
      presets: [SwaggerUIBundle.presets.apis],
      layout: 'BaseLayout'
    });
  </script>
</body>
</html>
""";
                return Results.Content(html, "text/html; charset=utf-8");
            }).ExcludeFromDescription();
        }

        return app;
    }
}
