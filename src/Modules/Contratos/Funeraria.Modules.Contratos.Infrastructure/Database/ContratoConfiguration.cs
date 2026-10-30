using Funeraria.Modules.Contratos.Domain.Contratos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Funeraria.Modules.Contratos.Infrastructure.Database;

internal sealed class ContratoConfiguration : IEntityTypeConfiguration<Contrato>
{
    public void Configure(EntityTypeBuilder<Contrato> builder)
    {
        builder.ToTable("contratos");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id_contrato").ValueGeneratedNever();

        builder.Property(c => c.NomePlano).HasMaxLength(100);
        builder.Property(c => c.MotivoCancelamento).HasMaxLength(500);
        builder.Property(c => c.TipoCobertura).HasConversion<string>().HasMaxLength(30);
        builder.Property(c => c.Periodicidade).HasConversion<string>().HasMaxLength(10);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasMany(c => c.PessoasCobertas)
            .WithOne()
            .HasForeignKey("IdContrato")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.PessoasCobertas).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Um contrato vigente por cliente e plano (card Criar Contrato). Garante a regra mesmo com duas
        // contratações ao mesmo tempo; contrato cancelado não conta.
        builder.HasIndex(c => new { c.TenantId, c.IdCliente, c.IdPlano })
            .IsUnique()
            .HasFilter($"status <> '{nameof(StatusContrato.Cancelado)}'");

        builder.HasIndex(c => new { c.TenantId, c.Status });
    }
}

internal sealed class PessoaCobertaConfiguration : IEntityTypeConfiguration<PessoaCoberta>
{
    public void Configure(EntityTypeBuilder<PessoaCoberta> builder)
    {
        builder.ToTable("pessoas_cobertas");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id_pessoa_coberta").ValueGeneratedNever();
        builder.Property<Guid>("IdContrato").HasColumnName("id_contrato");
        builder.Property(p => p.Tipo).HasConversion<string>().HasMaxLength(20);

        // Um dependente fica em um contrato por vez. A saída (CON-02) e o cancelamento (CON-03) preenchem data_remocao.
        builder.HasIndex(p => p.IdDependente)
            .IsUnique()
            .HasFilter("id_dependente IS NOT NULL AND data_remocao IS NULL");
    }
}
