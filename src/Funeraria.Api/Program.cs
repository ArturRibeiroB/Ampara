using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Funeraria.Api.Identity;
using Funeraria.Api.Middlewares;
using Funeraria.Api.Modules;
using Funeraria.Api.OpenApi;
using Funeraria.BuildingBlocks.Infrastructure;
using Funeraria.BuildingBlocks.Infrastructure.Database;
using Funeraria.Modules.Tenants.Infrastructure.Database;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.ConfigureHttpJsonOptions(options =>
{
    // Contrato da API em snake_case, como nos cards (ex.: valor_mensal, access_token).
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
});

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Instance = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
    context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddOptions<TenancyOptions>().Bind(builder.Configuration.GetSection(TenancyOptions.Section));
builder.Services.AddAutenticacao(builder.Configuration);
builder.Services.AddDocumentacaoApi();

builder.Services.AddBuildingBlocks(builder.Configuration);
builder.Services.AddModules(builder.Configuration);

WebApplication app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapDocumentacaoApi();
}

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

app.MapModuleEndpoints();

if (app.Configuration.GetValue<bool>("Database:AplicarMigrations"))
{
    await app.Services.ApplyModuleMigrationsAsync();
}

if (app.Configuration.GetValue<bool>("Database:SeedDemo"))
{
    await app.Services.SeedDemoAsync();
}

await app.RunAsync();

/// <summary>Exposto para o WebApplicationFactory dos testes de integração.</summary>
public partial class Program;
