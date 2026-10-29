using Funeraria.BuildingBlocks.Application.Arquivos;
using Funeraria.BuildingBlocks.Infrastructure.Jobs;
using Funeraria.BuildingBlocks.Infrastructure.Modules;
using Funeraria.Modules.Documentos.Application.Certidoes;
using Funeraria.Modules.Documentos.Domain.Certidoes;
using Funeraria.Modules.Documentos.Application.Documentos;
using Funeraria.Modules.Documentos.Domain.Documentos;
using Funeraria.Modules.Documentos.Infrastructure.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Funeraria.Modules.Documentos.Infrastructure;

public static class DocumentosModule
{
    public static IServiceCollection AddDocumentosModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleApplication(Application.AssemblyReference.Assembly);
        services.AddModuleDbContext<DocumentosDbContext>(DocumentosDbContext.SchemaName);
        services.AddOptions<DocumentosOptions>().Bind(configuration.GetSection(DocumentosOptions.Section));
        services.AddSingleton<IConfiguracaoDocumentos>(sp => sp.GetRequiredService<IOptions<DocumentosOptions>>().Value);

        // Documentos do falecido (CER-01).
        services.AddScoped<IDocumentosUnitOfWork>(sp => sp.GetRequiredService<DocumentosDbContext>());
        services.AddScoped<IDocumentoRepository, DocumentoRepository>();
        services.AddScoped<AcessoAosDocumentos>();
        services.AddScoped<GravacaoDeDocumentos>();
        services.AddScoped<LinksDeDocumentos>();

        // Certidão de óbito (CER-02 a CER-04). Novos modos (e-Óbito, segunda via) entram como outro ICertidaoObitoGateway.
        services.AddScoped<ISolicitacaoCertidaoRepository, SolicitacaoCertidaoRepository>();
        services.AddScoped<ICertidaoObitoGateway, CertidaoManualGateway>();
        services.AddScoped<ICertidaoObitoGateway, CertidaoSimuladaGateway>();
        services.AddScoped<SeletorGatewayCertidao>();
        services.AddScoped<RegistroDeCertidao>();
        services.AddScoped<AlertaPrazoCertidoes>();
        services.AddScoped<IJobRecorrente, AlertaPrazoCertidoesJob>();

        return services;
    }
}

/// <summary>Seção "Documentos" da configuração (CER-01 a CER-04).</summary>
public sealed class DocumentosOptions : IConfiguracaoDocumentos
{
    public const string Section = "Documentos";

    public int TamanhoMaximoMb { get; set; } = 10;

    public int ValidadeLinkMinutos { get; set; } = 5;

    public int PrazoRegistroObitoDias { get; set; } = 15;

    public bool PermitirModoSimulado { get; set; }

    public PoliticaArquivo Politica => PoliticaArquivo.Documento with { TamanhoMaximo = TamanhoMaximoMb * 1024L * 1024L };

    public TimeSpan ValidadeLink => TimeSpan.FromMinutes(ValidadeLinkMinutos);
}
