using System.Reflection;
using System.Text.Json.Nodes;
using Funeraria.Api.Middlewares;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Funeraria.Api.OpenApi;

/// <summary>Documento OpenAPI v1 com JWT Bearer (INF-09). É o contrato com o repositório do frontend (INF-21).</summary>
internal static class OpenApiExtensions
{
    /// <summary>
    /// A API foi carregada só para gerar o openapi.json no build (dotnet-getdocument, INF-21). Nesse caso não há banco:
    /// migrations, seed e agendamento de jobs ficam de fora.
    /// </summary>
    public static bool GerandoDocumentoNoBuild =>
        string.Equals(Assembly.GetEntryAssembly()?.GetName().Name, "GetDocument.Insider", StringComparison.Ordinal);

    private const string Titulo = "Funerária Online API";

    /// <summary>
    /// Versão do contrato no <c>info.version</c>: a da tag do release (vX.Y.Z → X.Y.Z), passada no build pelo CI.
    /// Fora de um release fica <c>X.Y.Z-dev</c> (INF-21). O metadado de build (+commit) fica de fora.
    /// </summary>
    public static string VersaoContrato { get; } =
        typeof(OpenApiExtensions).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "0.0.0";

    public static IServiceCollection AddDocumentacaoApi(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = Titulo;
                document.Info.Version = VersaoContrato;
                document.Info.Description =
                    "Para testar pelo Swagger: informe o X-Tenant (ex.: demo), faça POST /api/v1/auth/login " +
                    "e cole o access_token em Authorize.";
                return Task.CompletedTask;
            });
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
            options.AddOperationTransformer<TenantHeaderTransformer>();
            options.AddOperationTransformer<ObsoletoTransformer>();
            options.AddSchemaTransformer<ObsoletoTransformer>();
        });

        return services;
    }

    /// <summary>
    /// /openapi/v1.json, Swagger UI em /swagger e Scalar em /scalar.
    /// Ligado em Development; nos outros ambientes só com <c>Documentacao:Habilitada = true</c> (INF-09).
    /// </summary>
    public static WebApplication MapDocumentacaoApi(this WebApplication app)
    {
        if (!app.Configuration.GetValue("Documentacao:Habilitada", app.Environment.IsDevelopment()))
        {
            return app;
        }

        app.MapOpenApi().AllowAnonymous();

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", $"{Titulo} v1");
            options.RoutePrefix = "swagger";
            options.DocumentTitle = Titulo;
            options.EnablePersistAuthorization();
            options.DisplayRequestDuration();
        });

        app.MapScalarApiReference(options => options.WithTitle(Titulo)).AllowAnonymous();

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

    /// <summary>
    /// Marca como <c>deprecated</c> o que tem <see cref="ObsoleteAttribute"/>: endpoint com <c>.MarcarObsoleto()</c> e
    /// propriedade de request/response com <c>[property: Obsolete]</c>. Obsoleto antes de remover (INF-21).
    /// </summary>
    private sealed class ObsoletoTransformer : IOpenApiOperationTransformer, IOpenApiSchemaTransformer
    {
        public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
        {
            if (context.Description.ActionDescriptor.EndpointMetadata.OfType<ObsoleteAttribute>().Any())
            {
                operation.Deprecated = true;
            }

            return Task.CompletedTask;
        }

        public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
        {
            if (context.JsonPropertyInfo?.AttributeProvider?.IsDefined(typeof(ObsoleteAttribute), inherit: true) == true)
            {
                schema.Deprecated = true;
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Documenta o cabeçalho X-Tenant em todas as operações. No navegador o tenant vem do subdomínio;
    /// pelo Swagger (localhost) é por esse cabeçalho que se escolhe a funerária (TEN-01).
    /// </summary>
    private sealed class TenantHeaderTransformer : IOpenApiOperationTransformer
    {
        public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = TenantResolutionMiddleware.TenantHeader,
                In = ParameterLocation.Header,
                Required = false,
                Description = "Slug da funerária (ex.: demo). Vazio = domínio da plataforma (login do ADMIN).",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
                Example = JsonValue.Create("demo")
            });

            return Task.CompletedTask;
        }
    }
}
