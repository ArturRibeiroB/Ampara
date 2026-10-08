using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Funeraria.Api.OpenApi;

/// <summary>Documento OpenAPI v1 com JWT Bearer (INF-09). É o contrato com o repositório do frontend (INF-21).</summary>
internal static class OpenApiExtensions
{
    public static IServiceCollection AddDocumentacaoApi(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "Funerária Online API";
                document.Info.Version = "v1";
                return Task.CompletedTask;
            });
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
        });

        return services;
    }

    /// <summary>Em Development: /openapi/v1.json e a UI do Scalar em /scalar.</summary>
    public static WebApplication MapDocumentacaoApi(this WebApplication app)
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options => options.WithTitle("Funerária Online API"));
        return app;
    }

    private sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
    {
        private const string Scheme = "Bearer";

        public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[Scheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Access token JWT obtido em POST /api/v1/auth/login."
            };

            document.Security ??= [];
            document.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(Scheme, document)] = []
            });

            return Task.CompletedTask;
        }
    }
}
