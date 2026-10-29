using System.Text.Json;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Serialization;
using Funeraria.BuildingBlocks.Infrastructure.Database;
using Funeraria.Modules.Funeral.Application.Abstractions;
using Funeraria.Modules.Funeral.Domain.Beneficiarios;
using Funeraria.Modules.Funeral.Domain.Preferencias;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using FuneralEntity = Funeraria.Modules.Funeral.Domain.Funerais.Funeral;

namespace Funeraria.Modules.Funeral.Infrastructure.Database;

public sealed class FuneralDbContext(DbContextOptions<FuneralDbContext> options, ITenantContext tenantContext, ICriptografiaDados criptografia)
    : ModuleDbContext(options, tenantContext), IFuneralUnitOfWork
{
    public const string SchemaName = "funeral";

    public override string Schema => SchemaName;

    internal DbSet<PreferenciaFuneral> Preferencias => Set<PreferenciaFuneral>();

    internal DbSet<Beneficiario> Beneficiarios => Set<Beneficiario>();

    internal DbSet<FuneralEntity> Funerais => Set<FuneralEntity>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FuneralDbContext).Assembly);

        // Crença religiosa é dado sensível (LGPD): cifrada em repouso (SEG-01).
        modelBuilder.Entity<PreferenciaFuneral>().Property(p => p.CrencaReligiosa).HasConversion(
            new ValueConverter<string?, string?>(v => v == null ? null : criptografia.Cifrar(v), v => v == null ? null : criptografia.Decifrar(v)));
    }
}

internal sealed class PreferenciaFuneralConfiguration : IEntityTypeConfiguration<PreferenciaFuneral>
{
    public void Configure(EntityTypeBuilder<PreferenciaFuneral> builder)
    {
        builder.ToTable("preferencias");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id_preferencia").ValueGeneratedNever();
        builder.Property(p => p.TipoPessoa).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Destinacao).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.TipoCerimonia).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.CrencaReligiosa).HasMaxLength(500);
        builder.Property(p => p.Mensagens).HasMaxLength(2000);
        builder.Property(p => p.Observacoes).HasMaxLength(2000);

        builder.Property(p => p.Musicas)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonbOptions.Default),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonbOptions.Default) ?? new List<string>(),
                Comparador<string>());

        builder.Property(p => p.Itens)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonbOptions.Default),
                v => JsonSerializer.Deserialize<List<ItemPreferido>>(v, JsonbOptions.Default) ?? new List<ItemPreferido>(),
                Comparador<ItemPreferido>());

        // Uma preferência por pessoa.
        builder.HasIndex(p => new { p.TenantId, p.IdPessoa }).IsUnique();
        builder.HasIndex(p => p.IdCliente);
    }

    private static ValueComparer<IReadOnlyList<T>> Comparador<T>() =>
        new((a, b) => a!.SequenceEqual(b!), v => v.Aggregate(0, (h, x) => HashCode.Combine(h, x)), v => v.ToList());
}

internal sealed class PreferenciaRepository(FuneralDbContext context) : IPreferenciaRepository
{
    public async Task<IReadOnlyList<PreferenciaFuneral>> ListarDoTitularAsync(Guid idCliente, CancellationToken cancellationToken = default) =>
        await context.Preferencias.AsNoTracking().Where(p => p.IdCliente == idCliente).ToListAsync(cancellationToken);

    public Task<PreferenciaFuneral?> ObterDaPessoaAsync(Guid idCliente, Guid idPessoa, CancellationToken cancellationToken = default) =>
        context.Preferencias.FirstOrDefaultAsync(p => p.IdCliente == idCliente && p.IdPessoa == idPessoa, cancellationToken);

    public void Adicionar(PreferenciaFuneral preferencia) => context.Preferencias.Add(preferencia);
}
