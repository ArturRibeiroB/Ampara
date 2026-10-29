using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Infrastructure.Database;
using Funeraria.Modules.Documentos.Application.Documentos;
using Funeraria.Modules.Documentos.Domain.Documentos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Funeraria.Modules.Documentos.Infrastructure.Database;

public sealed class DocumentosDbContext(DbContextOptions<DocumentosDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext), IDocumentosUnitOfWork
{
    public const string SchemaName = "documentos";

    public override string Schema => SchemaName;

    internal DbSet<Documento> Documentos => Set<Documento>();

    protected override void ConfigureModel(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocumentosDbContext).Assembly);
}

internal sealed class DocumentoConfiguration : IEntityTypeConfiguration<Documento>
{
    public void Configure(EntityTypeBuilder<Documento> builder)
    {
        builder.ToTable("documentos");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id_documento").ValueGeneratedNever();
        builder.Property(d => d.Tipo).HasConversion<string>().HasMaxLength(30);
        builder.Property(d => d.NomeOriginal).HasMaxLength(255);
        builder.Property(d => d.Chave).HasMaxLength(300);
        builder.Property(d => d.ContentType).HasMaxLength(100);

        builder.HasIndex(d => d.IdBeneficiario);
    }
}

internal sealed class DocumentoRepository(DocumentosDbContext context) : IDocumentoRepository
{
    public Task<Documento?> ObterAsync(Guid idDocumento, CancellationToken cancellationToken = default) =>
        context.Documentos.FirstOrDefaultAsync(d => d.Id == idDocumento, cancellationToken);

    public async Task<IReadOnlyList<Documento>> ListarDoBeneficiarioAsync(Guid idBeneficiario, CancellationToken cancellationToken = default) =>
        await context.Documentos.AsNoTracking().Where(d => d.IdBeneficiario == idBeneficiario).OrderBy(d => d.EnviadoEm).ToListAsync(cancellationToken);

    public void Adicionar(Documento documento) => context.Documentos.Add(documento);

    public void Remover(Documento documento) => context.Documentos.Remove(documento);
}
