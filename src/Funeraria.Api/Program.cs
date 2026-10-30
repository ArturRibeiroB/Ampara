using System.Text.Json;
using System.Text.Json.Serialization;
using Funeraria.Api.HealthChecks;
using Funeraria.Api.Identity;
using Funeraria.Api.Middlewares;
using Funeraria.Api.Modules;
using Funeraria.Api.OpenApi;
using Funeraria.BuildingBlocks.Infrastructure;
using Funeraria.BuildingBlocks.Infrastructure.Database;
using Funeraria.BuildingBlocks.Infrastructure.Jobs;
using Funeraria.BuildingBlocks.Infrastructure.Logging;
using Funeraria.BuildingBlocks.Infrastructure.Seeding;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

if (OpenApiExtensions.GerandoDocumentoNoBuild)
{
    // Só para o host montar ao gerar o openapi.json no build: a API não atende requisições nesse modo.
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Jwt:ChaveAssinatura"] = Convert.ToBase64String(new byte[64]),
        ["Seguranca:ChaveHash"] = Convert.ToBase64String(new byte[32])
    });
}

builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.With<MascaraDadosSensiveis>());

builder.Services.ConfigureHttpJsonOptions(options =>
{
    // Contrato da API em snake_case, como nos cards (ex.: valor_mensal, access_token).
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
});

// O ASP.NET já inclui o trace id em todo ProblemDetails; com a política snake_case ele sai como "trace_id".
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Instance = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}");
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddOptions<TenancyOptions>().Bind(builder.Configuration.GetSection(TenancyOptions.Section));
builder.Services.AddAutorizacao();
builder.Services.AddDocumentacaoApi();
builder.Services.AddVerificacoesDeSaude();
builder.Services.AddLimiteDeRequisicoes(builder.Configuration);
builder.Services.AddSeguranca(builder.Configuration);

builder.Services.AddBuildingBlocks(builder.Configuration);
builder.Services.AddModules(builder.Configuration);
builder.Services.AddJobs(builder.Configuration);
builder.Services.AddPainelJobs();

WebApplication app = builder.Build();

app.UseSeguranca();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();

app.MapDocumentacaoApi();

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

app.MapModuleEndpoints();
app.MapPainelJobs();
app.MapVerificacoesDeSaude();

if (!OpenApiExtensions.GerandoDocumentoNoBuild)
{
    if (app.Configuration.GetValue<bool>("Database:AplicarMigrations"))
    {
        await app.Services.ApplyModuleMigrationsAsync();
    }

    if (app.Configuration.GetValue<bool>("Seed:Executar"))
    {
        await app.Services.SeedModulesAsync();
    }

    await app.Services.AgendarJobsRecorrentesAsync();
}

await app.RunAsync();

/// <summary>Exposto para o WebApplicationFactory dos testes de integração.</summary>
public partial class Program;
