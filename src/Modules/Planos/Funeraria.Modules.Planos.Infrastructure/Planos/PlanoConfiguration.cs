using System.Text.Json;
using Funeraria.BuildingBlocks.Application.Serialization;
using Funeraria.Modules.Planos.Domain.Planos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Funeraria.Modules.Planos.Infrastructure.Planos;

internal sealed class PlanoConfiguration : IEntityTypeConfiguration<Plano>
{
    public void Configure(EntityTypeBuilder<Plano> builder)
    {
        builder.ToTable("planos");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id_plano").ValueGeneratedNever();

        builder.Property(p => p.Nome).HasMaxLength(100);
        builder.Property(p => p.Descricao).HasMaxLength(1000);
        builder.Property(p => p.IdStripeProduct).HasMaxLength(100);

        builder.ComplexProperty(p => p.Precos, precos =>
        {
            precos.Property(x => x.ValorMensal).HasColumnName("valor_mensal");
            precos.Property(x => x.ValorAnual).HasColumnName("valor_anual");
            precos.Property(x => x.ValorDependenteMensal).HasColumnName("valor_dependente_mensal");
            precos.Property(x => x.ValorDependenteAnual).HasColumnName("valor_dependente_anual");
        });

        // jsonb tipado (PLA-03): chaves em snake_case, iguais às dos cards.
        builder.Property(p => p.Configuracoes)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonbOptions.Default),
                v => JsonSerializer.Deserialize<PlanoConfiguracoes>(v, JsonbOptions.Default) ?? PlanoConfiguracoes.Padrao);

        builder.HasIndex(p => new { p.TenantId, p.Nome }).IsUnique();
    }
}
