using Funeraria.BuildingBlocks.Domain.Estados;
using Funeraria.BuildingBlocks.Infrastructure.Jobs;
using Funeraria.Modules.Documentos.Application.Certidoes;
using Funeraria.Modules.Documentos.Domain.Certidoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Logging;

namespace Funeraria.Modules.Documentos.Infrastructure.Database;

internal sealed class SolicitacaoCertidaoConfiguration : IEntityTypeConfiguration<SolicitacaoCertidao>
{
    public void Configure(EntityTypeBuilder<SolicitacaoCertidao> builder)
    {
        builder.ToTable("solicitacoes_certidao");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id_solicitacao").ValueGeneratedNever();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Modo).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.TipoDoResponsavel).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.NomeFalecido).HasMaxLength(150);
        builder.Property(s => s.NomeResponsavel).HasMaxLength(200);
        builder.Property(s => s.Protocolo).HasMaxLength(100);
        builder.Property(s => s.Pendencia).HasMaxLength(1000);
        builder.Property(s => s.NumeroMatricula).HasMaxLength(60);

        builder.HasMany(s => s.Historico).WithOne().HasForeignKey("IdSolicitacao").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Historico).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();

        // Uma solicitação por beneficiário (CER-03): em andamento ou já emitida.
        builder.HasIndex(s => s.IdBeneficiario).IsUnique();
        builder.HasIndex(s => new { s.Status, s.PrazoRegistro });
    }
}

internal sealed class HistoricoCertidaoConfiguration : IEntityTypeConfiguration<HistoricoStatus<StatusCertidao>>
{
    public void Configure(EntityTypeBuilder<HistoricoStatus<StatusCertidao>> builder)
    {
        builder.ToTable("solicitacoes_certidao_historico");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnName("id_historico").ValueGeneratedNever();
        builder.Property<Guid>("IdSolicitacao").HasColumnName("id_solicitacao");
        builder.Property(h => h.De).HasConversion<string>().HasMaxLength(20);
        builder.Property(h => h.Para).HasConversion<string>().HasMaxLength(20);
        builder.Property(h => h.Acao).HasMaxLength(40);
        builder.Property(h => h.Observacao).HasMaxLength(1000);
    }
}

internal sealed class SolicitacaoCertidaoRepository(DocumentosDbContext context) : ISolicitacaoCertidaoRepository
{
    public Task<SolicitacaoCertidao?> ObterAsync(Guid idSolicitacao, CancellationToken cancellationToken = default) =>
        context.Solicitacoes.FirstOrDefaultAsync(s => s.Id == idSolicitacao, cancellationToken);

    public Task<SolicitacaoCertidao?> ObterDoBeneficiarioAsync(Guid idBeneficiario, CancellationToken cancellationToken = default) =>
        context.Solicitacoes.FirstOrDefaultAsync(s => s.IdBeneficiario == idBeneficiario, cancellationToken);

    // O job roda fora de uma requisição: olha todas as funerárias, e cada aviso leva o tenant da solicitação.
    public async Task<IReadOnlyList<SolicitacaoCertidao>> ListarComPrazoAteAsync(DateOnly data, CancellationToken cancellationToken = default) =>
        await context.Solicitacoes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => s.Status != StatusCertidao.Emitida && s.PrazoRegistro <= data)
            .ToListAsync(cancellationToken);

    public void Adicionar(SolicitacaoCertidao solicitacao) => context.Solicitacoes.Add(solicitacao);
}

/// <summary>Todo dia às 11:00 UTC (8h em Brasília): alerta do prazo legal do registro do óbito (CER-03).</summary>
internal sealed partial class AlertaPrazoCertidoesJob(AlertaPrazoCertidoes alerta, ILogger<AlertaPrazoCertidoesJob> logger) : IJobRecorrente
{
    public string Id => "documentos-alerta-prazo-certidao";

    public string Cron => "0 11 * * *";

    public async Task ExecutarAsync(CancellationToken cancellationToken) =>
        LogAlertas(logger, await alerta.ExecutarAsync(cancellationToken));

    [LoggerMessage(Level = LogLevel.Information, Message = "Alerta de prazo de certidão: {Quantidade} solicitações avisadas")]
    private static partial void LogAlertas(ILogger logger, int quantidade);
}
