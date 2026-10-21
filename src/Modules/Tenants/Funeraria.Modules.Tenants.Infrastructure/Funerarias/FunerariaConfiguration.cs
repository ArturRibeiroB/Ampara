using System.Text.Json;
using Funeraria.BuildingBlocks.Application.Serialization;
using Funeraria.Modules.Tenants.Domain.Funerarias;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FunerariaEntity = Funeraria.Modules.Tenants.Domain.Funerarias.Funeraria;

namespace Funeraria.Modules.Tenants.Infrastructure.Funerarias;

internal sealed class FunerariaConfiguration : IEntityTypeConfiguration<FunerariaEntity>
{
    public void Configure(EntityTypeBuilder<FunerariaEntity> builder)
    {
        builder.ToTable("funerarias");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id_tenant").ValueGeneratedNever();

        builder.Property(f => f.NomeFantasia).HasMaxLength(150);
        builder.Property(f => f.RazaoSocial).HasMaxLength(150);
        builder.Property(f => f.Cnpj).HasMaxLength(14).IsFixedLength();
        builder.Property(f => f.Slug).HasMaxLength(63);
        builder.Property(f => f.EmailContato).HasMaxLength(254);
        builder.Property(f => f.Telefone).HasMaxLength(20);
        builder.Property(f => f.IdStripeAccount).HasMaxLength(100);

        builder.Property(f => f.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(f => f.OrigemCadastro).HasConversion<string>().HasMaxLength(20);

        // jsonb tipado (TEN-03), com chaves em snake_case. '{}' nas linhas antigas vira os valores padrão.
        builder.Property(f => f.IdentidadeVisual)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonbOptions.Default),
                v => JsonSerializer.Deserialize<IdentidadeVisual>(v, JsonbOptions.Default) ?? new IdentidadeVisual());

        builder.Property(f => f.Parametros)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonbOptions.Default),
                v => JsonSerializer.Deserialize<ParametrosFuneraria>(v, JsonbOptions.Default) ?? new ParametrosFuneraria());

        // CNPJ e slug são únicos na plataforma (TEN-02).
        builder.HasIndex(f => f.Cnpj).IsUnique();
        builder.HasIndex(f => f.Slug).IsUnique();
    }
}
