using System.Text.Json;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Paginacao;
using Funeraria.BuildingBlocks.Application.Serialization;
using Funeraria.BuildingBlocks.Infrastructure.Database;
using Funeraria.Modules.Terceiros.Application.Abstractions;
using Funeraria.Modules.Terceiros.Domain.Atribuicoes;
using Funeraria.Modules.Terceiros.Domain.Terceiros;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Funeraria.Modules.Terceiros.Infrastructure.Database;

public sealed class TerceirosDbContext(DbContextOptions<TerceirosDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext), ITerceirosUnitOfWork
{
    public const string SchemaName = "terceiros";

    public override string Schema => SchemaName;

    internal DbSet<Terceiro> Terceiros => Set<Terceiro>();

    internal DbSet<Atribuicao> Atribuicoes => Set<Atribuicao>();

    protected override void ConfigureModel(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TerceirosDbContext).Assembly);
}

internal sealed class TerceiroConfiguration : IEntityTypeConfiguration<Terceiro>
{
    public void Configure(EntityTypeBuilder<Terceiro> builder)
    {
        builder.ToTable("terceiros");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id_terceiro").ValueGeneratedNever();
        builder.Property(t => t.Tipo).HasConversion<string>().HasMaxLength(30);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.NomeRazaoSocial).HasMaxLength(150);
        builder.Property(t => t.CpfCnpj).HasMaxLength(14);
        builder.Property(t => t.Telefone).HasMaxLength(20);
        builder.Property(t => t.Email).HasMaxLength(254);

        // jsonb tipado, com chaves em snake_case.
        builder.Property(t => t.AreaAtuacao)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonbOptions.Default),
                v => JsonSerializer.Deserialize<AreaAtuacao>(v, JsonbOptions.Default) ?? new AreaAtuacao());

        builder.Property(t => t.CategoriasAtendidas)
            .HasColumnType("uuid[]")
            .HasConversion(v => v.ToArray(), v => v.ToList(), new ValueComparer<IReadOnlyList<Guid>>(
                (a, b) => a!.SequenceEqual(b!), v => v.Aggregate(0, (h, g) => HashCode.Combine(h, g)), v => v.ToList()));

        // CPF/CNPJ e e-mail únicos na funerária (TER-01): o mesmo parceiro pode atender várias funerárias.
        builder.HasIndex(t => new { t.TenantId, t.CpfCnpj }).IsUnique();
        builder.HasIndex(t => new { t.TenantId, t.Email }).IsUnique();
        builder.HasIndex(t => t.IdUsuario);
    }
}

internal sealed class AtribuicaoConfiguration : IEntityTypeConfiguration<Atribuicao>
{
    public void Configure(EntityTypeBuilder<Atribuicao> builder)
    {
        builder.ToTable("atribuicoes");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id_atribuicao").ValueGeneratedNever();
        builder.Property(a => a.Tipo).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Resumo).HasColumnType("jsonb");
        builder.Property(a => a.MotivoRecusa).HasMaxLength(500);

        builder.HasOne<Terceiro>().WithMany().HasForeignKey(a => a.IdTerceiro).OnDelete(DeleteBehavior.Restrict);

        // Uma atribuição por serviço de origem e terceiro: o módulo de origem pode repetir a chamada sem duplicar.
        builder.HasIndex(a => new { a.IdTerceiro, a.Tipo, a.IdOrigem }).IsUnique();
    }
}

internal sealed class AtribuicaoRepository(TerceirosDbContext context) : IAtribuicaoRepository
{
    public Task<Atribuicao?> ObterAsync(Guid idAtribuicao, CancellationToken cancellationToken = default) =>
        context.Atribuicoes.FirstOrDefaultAsync(a => a.Id == idAtribuicao, cancellationToken);

    public Task<Atribuicao?> ObterPorOrigemAsync(Guid idTerceiro, TipoAtribuicao tipo, Guid idOrigem, CancellationToken cancellationToken = default) =>
        context.Atribuicoes.FirstOrDefaultAsync(a => a.IdTerceiro == idTerceiro && a.Tipo == tipo && a.IdOrigem == idOrigem, cancellationToken);

    public async Task<IReadOnlyList<Atribuicao>> ListarDoTerceiroAsync(Guid idTerceiro, CancellationToken cancellationToken = default) =>
        await context.Atribuicoes.AsNoTracking()
            .Where(a => a.IdTerceiro == idTerceiro)
            .OrderByDescending(a => a.Id)
            .ToListAsync(cancellationToken);

    public void Adicionar(Atribuicao atribuicao) => context.Atribuicoes.Add(atribuicao);
}

internal sealed class TerceiroRepository(TerceirosDbContext context) : ITerceiroRepository, ITerceirosLeitura
{
    public Task<Terceiro?> ObterAsync(Guid idTerceiro, CancellationToken cancellationToken = default) =>
        context.Terceiros.FirstOrDefaultAsync(t => t.Id == idTerceiro, cancellationToken);

    public async Task<IReadOnlyList<Terceiro>> ListarPorTipoAsync(TipoTerceiro tipo, CancellationToken cancellationToken = default) =>
        await context.Terceiros.AsNoTracking().Where(t => t.Tipo == tipo).OrderBy(t => t.NomeRazaoSocial).ToListAsync(cancellationToken);

    public Task<bool> ExisteCpfCnpjAsync(string cpfCnpj, Guid? exceto, CancellationToken cancellationToken = default) =>
        context.Terceiros.AnyAsync(t => t.CpfCnpj == cpfCnpj && t.Id != exceto, cancellationToken);

    public Task<bool> ExisteEmailAsync(string email, CancellationToken cancellationToken = default) =>
        context.Terceiros.AnyAsync(t => t.Email.ToLower() == email.ToLower(), cancellationToken);

    // A conta é da plataforma: o evento chega sem tenant e atualiza o parceiro em todas as funerárias.
    public async Task<IReadOnlyList<Terceiro>> ListarDaContaEmTodasAsFunerariasAsync(Guid idUsuario, CancellationToken cancellationToken = default) =>
        await context.Terceiros.IgnoreQueryFilters().Where(t => t.IdUsuario == idUsuario).ToListAsync(cancellationToken);

    public void Adicionar(Terceiro terceiro) => context.Terceiros.Add(terceiro);

    public async Task<(IReadOnlyList<Terceiro> Itens, long Total)> ListarAsync(
        TipoTerceiro? tipo,
        StatusTerceiro? status,
        PaginaRequest pagina,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Terceiro> consulta = context.Terceiros.AsNoTracking()
            .Where(t => tipo == null || t.Tipo == tipo)
            .Where(t => status == null || t.Status == status);

        long total = await consulta.LongCountAsync(cancellationToken);
        List<Terceiro> itens = await consulta
            .OrderBy(t => t.NomeRazaoSocial).ThenBy(t => t.Id)
            .Skip(pagina.Deslocamento).Take(pagina.Tamanho)
            .ToListAsync(cancellationToken);
        return (itens, total);
    }
}
