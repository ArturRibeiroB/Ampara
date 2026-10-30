using Funeraria.Modules.Identidade.Application.Termos;
using Funeraria.Modules.Identidade.Domain.Termos;
using Funeraria.Modules.Identidade.Infrastructure.Database;
using Funeraria.Modules.Identidade.Infrastructure.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Options;

namespace Funeraria.Modules.Identidade.Infrastructure.Termos;

internal sealed class AceiteTermosConfiguration : IEntityTypeConfiguration<AceiteTermos>
{
    public void Configure(EntityTypeBuilder<AceiteTermos> builder)
    {
        builder.ToTable("aceites_termos");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id_aceite").ValueGeneratedNever();
        builder.Property(a => a.Documento).HasConversion<string>().HasMaxLength(30);
        builder.Property(a => a.Versao).HasMaxLength(20);
        builder.Property(a => a.Ip).HasMaxLength(45);

        builder.HasOne<Usuario>().WithMany().HasForeignKey(a => a.IdUsuario).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(a => a.IdUsuario);
    }
}

internal sealed class AceiteTermosRepository(IdentidadeDbContext context) : IAceiteTermosRepository
{
    public async Task<IReadOnlyList<AceiteTermos>> ListarDoUsuarioAsync(Guid idUsuario, CancellationToken cancellationToken = default) =>
        await context.Set<AceiteTermos>().AsNoTracking().Where(a => a.IdUsuario == idUsuario).ToListAsync(cancellationToken);

    public void Adicionar(AceiteTermos aceite) => context.Set<AceiteTermos>().Add(aceite);
}

internal sealed class TermosVigentes(IOptions<LgpdOptions> options) : ITermosVigentes
{
    public string VersaoTermosDeUso => options.Value.VersaoTermosDeUso;

    public string VersaoPoliticaDePrivacidade => options.Value.VersaoPoliticaDePrivacidade;
}

/// <summary>Seção <c>Lgpd</c>. Publicar um texto novo é trocar a versão aqui: todos passam a ter o aceite pendente.</summary>
public sealed class LgpdOptions
{
    public const string Section = "Lgpd";

    public string VersaoTermosDeUso { get; set; } = "1.0";

    public string VersaoPoliticaDePrivacidade { get; set; } = "1.0";
}
