using Funeraria.BuildingBlocks.Domain.Estados;
using Funeraria.Modules.Funeral.Application.Funerais;
using Funeraria.Modules.Funeral.Domain.Beneficiarios;
using Funeraria.Modules.Funeral.Domain.Funerais;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FuneralEntity = Funeraria.Modules.Funeral.Domain.Funerais.Funeral;

namespace Funeraria.Modules.Funeral.Infrastructure.Database;

internal sealed class BeneficiarioConfiguration : IEntityTypeConfiguration<Beneficiario>
{
    public void Configure(EntityTypeBuilder<Beneficiario> builder)
    {
        builder.ToTable("beneficiarios");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("id_beneficiario").ValueGeneratedNever();
        builder.Property(b => b.Nome).HasMaxLength(150);
        builder.Property(b => b.LocalFalecimento).HasMaxLength(200);
        builder.ComplexProperty(b => b.Responsavel, r =>
        {
            r.Property(x => x.Nome).HasColumnName("responsavel_nome").HasMaxLength(150);
            r.Property(x => x.Telefone).HasColumnName("responsavel_telefone").HasMaxLength(20);
            r.Property(x => x.Parentesco).HasColumnName("responsavel_parentesco").HasMaxLength(50);
        });

        // Uma pessoa coberta só falece uma vez.
        builder.HasIndex(b => b.IdPessoaCoberta).IsUnique();
        builder.HasIndex(b => b.IdCliente);
    }
}

internal sealed class FuneralConfiguration : IEntityTypeConfiguration<FuneralEntity>
{
    public void Configure(EntityTypeBuilder<FuneralEntity> builder)
    {
        builder.ToTable("funerais");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id_funeral").ValueGeneratedNever();
        builder.Property(f => f.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(f => f.Destinacao).HasConversion<string>().HasMaxLength(20);
        builder.Property(f => f.NomeFalecido).HasMaxLength(150);
        builder.Property(f => f.Observacoes).HasMaxLength(2000);

        builder.HasOne<Beneficiario>().WithMany().HasForeignKey(f => f.IdBeneficiario).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(f => f.Historico).WithOne().HasForeignKey("IdFuneral").OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(f => f.Equipe).WithOne().HasForeignKey("IdFuneral").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(f => f.Equipe).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
        builder.Navigation(f => f.Historico).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();

        // Um funeral não cancelado por beneficiário; cancelado, a equipe pode abrir outro.
        builder.HasIndex(f => f.IdBeneficiario).IsUnique().HasFilter($"status <> '{nameof(StatusFuneral.Cancelado)}'");
        builder.HasIndex(f => new { f.TenantId, f.Status, f.DataHoraPrevista });
        builder.HasIndex(f => f.IdContrato);
    }
}

internal sealed class MembroEquipeFuneralConfiguration : IEntityTypeConfiguration<MembroEquipeFuneral>
{
    public void Configure(EntityTypeBuilder<MembroEquipeFuneral> builder)
    {
        builder.ToTable("funerais_equipe");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id_membro").ValueGeneratedNever();
        builder.Property<Guid>("IdFuneral").HasColumnName("id_funeral");
        builder.Property(m => m.Nome).HasMaxLength(150);
        builder.Property(m => m.Funcao).HasConversion<string>().HasMaxLength(30);

        builder.HasIndex("IdFuneral", nameof(MembroEquipeFuneral.IdUsuario)).IsUnique();
        builder.HasIndex(m => m.IdUsuario);
    }
}

internal sealed class HistoricoFuneralConfiguration : IEntityTypeConfiguration<HistoricoStatus<StatusFuneral>>
{
    public void Configure(EntityTypeBuilder<HistoricoStatus<StatusFuneral>> builder)
    {
        builder.ToTable("funerais_historico");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnName("id_historico").ValueGeneratedNever();
        builder.Property<Guid>("IdFuneral").HasColumnName("id_funeral");
        builder.Property(h => h.De).HasConversion<string>().HasMaxLength(20);
        builder.Property(h => h.Para).HasConversion<string>().HasMaxLength(20);
        builder.Property(h => h.Acao).HasMaxLength(40);
        builder.Property(h => h.Observacao).HasMaxLength(1000);
    }
}

internal sealed class BeneficiarioRepository(FuneralDbContext context) : IBeneficiarioRepository
{
    public Task<Beneficiario?> ObterAsync(Guid idBeneficiario, CancellationToken cancellationToken = default) =>
        context.Beneficiarios.FirstOrDefaultAsync(b => b.Id == idBeneficiario, cancellationToken);

    public Task<bool> ExisteDaPessoaAsync(Guid idPessoaCoberta, CancellationToken cancellationToken = default) =>
        context.Beneficiarios.AnyAsync(b => b.IdPessoaCoberta == idPessoaCoberta, cancellationToken);

    public void Adicionar(Beneficiario beneficiario) => context.Beneficiarios.Add(beneficiario);
}

internal sealed class FuneralRepository(FuneralDbContext context) : IFuneralRepository, IFuneraisLeitura
{
    public Task<FuneralEntity?> ObterAsync(Guid idFuneral, CancellationToken cancellationToken = default) =>
        context.Funerais.FirstOrDefaultAsync(f => f.Id == idFuneral, cancellationToken);

    public Task<bool> ExisteAtivoDoBeneficiarioAsync(Guid idBeneficiario, CancellationToken cancellationToken = default) =>
        context.Funerais.AnyAsync(f => f.IdBeneficiario == idBeneficiario && f.Status != StatusFuneral.Cancelado, cancellationToken);

    public Task<bool> ExisteEmAndamentoNoContratoAsync(Guid idContrato, CancellationToken cancellationToken = default) =>
        context.Funerais.AnyAsync(
            f => f.IdContrato == idContrato && f.Status != StatusFuneral.Concluido && f.Status != StatusFuneral.Cancelado,
            cancellationToken);

    public async Task<IReadOnlyList<FuneralEntity>> ListarAtribuidosAsync(Guid idUsuario, CancellationToken cancellationToken = default) =>
        await context.Funerais
            .AsNoTracking()
            .Where(f => f.Status != StatusFuneral.Concluido && f.Status != StatusFuneral.Cancelado)
            .Where(f => f.Equipe.Any(m => m.IdUsuario == idUsuario))
            .OrderBy(f => f.DataHoraPrevista)
            .ToListAsync(cancellationToken);

    public void Adicionar(FuneralEntity funeral) => context.Funerais.Add(funeral);

    public async Task<(IReadOnlyList<FuneralResumoResponse> Itens, long Total)> ListarAsync(
        StatusFuneral? status, DateTime? de, DateTime? ate, int deslocamento, int tamanho, CancellationToken cancellationToken)
    {
        IQueryable<FuneralEntity> consulta = context.Funerais.AsNoTracking().IgnoreAutoIncludes();
        if (status is not null)
        {
            consulta = consulta.Where(f => f.Status == status);
        }

        if (de is not null)
        {
            consulta = consulta.Where(f => f.DataHoraPrevista >= de);
        }

        if (ate is not null)
        {
            consulta = consulta.Where(f => f.DataHoraPrevista < ate);
        }

        long total = await consulta.LongCountAsync(cancellationToken);
        List<FuneralResumoResponse> itens = await consulta
            .OrderBy(f => f.DataHoraPrevista).ThenBy(f => f.Id)
            .Skip(deslocamento).Take(tamanho)
            .Select(f => new FuneralResumoResponse(f.Id, f.IdBeneficiario, f.NomeFalecido, f.Status, f.DataHoraPrevista, f.Destinacao))
            .ToListAsync(cancellationToken);
        return (itens, total);
    }
}
