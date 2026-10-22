using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FSIdentity.Api.Startup;

public static class ApiDocs
{
    public const string BearerScheme = "Bearer";

    public static OpenApiOptions AddBearerScheme(this OpenApiOptions options)
        => options.AddDocumentTransformer((document, _, _) =>
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "The accessToken from POST /api/Auth/login.",
            };

            document.Security ??= [];
            document.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(BearerScheme, document)] = [],
            });
            return Task.CompletedTask;
        });

    public static IEndpointConventionBuilder MapSwaggerPage(this IEndpointRouteBuilder app)
        => app.MapGet("/swagger", () => Results.Content(Page, "text/html; charset=utf-8")).AllowAnonymous().ExcludeFromDescription();

    private const string Page = """
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>FSIdentity API</title>
          <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/swagger-ui/5.17.14/swagger-ui.min.css">
        </head>
        <body>
          <div id="ui"></div>
          <script src="https://cdnjs.cloudflare.com/ajax/libs/swagger-ui/5.17.14/swagger-ui-bundle.min.js"></script>
          <script>
            window.ui = SwaggerUIBundle({ url: "/openapi/v1.json", dom_id: "#ui", persistAuthorization: true });
          </script>
        </body>
        </html>
        """;
}
