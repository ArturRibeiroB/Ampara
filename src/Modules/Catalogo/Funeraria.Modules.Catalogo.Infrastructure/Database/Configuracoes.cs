using Funeraria.Modules.Catalogo.Domain.Itens;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Funeraria.Modules.Catalogo.Infrastructure.Database;

internal sealed class CategoriaConfiguration : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> builder)
    {
        builder.ToTable("categorias");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id_categoria").ValueGeneratedNever();
        builder.Property(c => c.Nome).HasMaxLength(100);

        builder.HasIndex(c => new { c.TenantId, c.Nome }).IsUnique();
    }
}

internal sealed class ItemCatalogoConfiguration : IEntityTypeConfiguration<ItemCatalogo>
{
    public void Configure(EntityTypeBuilder<ItemCatalogo> builder)
    {
        builder.ToTable("itens");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id_item_catalogo").ValueGeneratedNever();
        builder.Property(i => i.Nome).HasMaxLength(150);
        builder.Property(i => i.Descricao).HasMaxLength(1000);
        builder.Property(i => i.ImagemChave).HasMaxLength(300);
        builder.Property(i => i.ImagemUrl).HasMaxLength(500);

        builder.HasOne<Categoria>().WithMany().HasForeignKey(i => i.IdCategoria).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(i => i.Variacoes).WithOne().HasForeignKey("IdItemCatalogo").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(i => i.Variacoes).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();

        builder.HasIndex(i => new { i.TenantId, i.IdCategoria, i.Nome }).IsUnique();
    }
}

internal sealed class VariacaoConfiguration : IEntityTypeConfiguration<Variacao>
{
    public void Configure(EntityTypeBuilder<Variacao> builder)
    {
        builder.ToTable("variacoes");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id_variacao").ValueGeneratedNever();
        builder.Property<Guid>("IdItemCatalogo").HasColumnName("id_item_catalogo");
        builder.Property(v => v.Nome).HasMaxLength(100);
    }
}
