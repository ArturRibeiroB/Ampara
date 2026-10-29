using System.Text.Json;
using Funeraria.BuildingBlocks.Application.Serialization;
using Funeraria.BuildingBlocks.Domain.Estados;
using Funeraria.Modules.Funeral.Domain.Pedidos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Funeraria.Modules.Funeral.Infrastructure.Database;

internal sealed class PedidoConfiguration : IEntityTypeConfiguration<Pedido>
{
    public void Configure(EntityTypeBuilder<Pedido> builder)
    {
        builder.ToTable("pedidos");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id_pedido").ValueGeneratedNever();
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.NomeFalecido).HasMaxLength(150);
        builder.Property(p => p.LocalEntrega).HasMaxLength(300);
        builder.Property(p => p.Observacoes).HasMaxLength(1000);
        builder.Property(p => p.MotivoRecusa).HasMaxLength(500);

        // Os itens são uma cópia do que o fornecedor recebe: ficam juntos, em jsonb.
        builder.Property(p => p.Itens)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonbOptions.Default),
                v => JsonSerializer.Deserialize<List<ItemDoPedido>>(v, JsonbOptions.Default) ?? new List<ItemDoPedido>(),
                new ValueComparer<IReadOnlyList<ItemDoPedido>>(
                    (a, b) => a!.SequenceEqual(b!), v => v.Aggregate(0, (h, x) => HashCode.Combine(h, x)), v => v.ToList()));

        builder.HasMany(p => p.Historico).WithOne().HasForeignKey("IdPedido").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Historico).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();

        builder.HasIndex(p => p.IdFuneral);
        builder.HasIndex(p => new { p.IdTerceiro, p.Status });
    }
}

internal sealed class HistoricoPedidoConfiguration : IEntityTypeConfiguration<HistoricoStatus<StatusPedido>>
{
    public void Configure(EntityTypeBuilder<HistoricoStatus<StatusPedido>> builder)
    {
        builder.ToTable("pedidos_historico");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnName("id_historico").ValueGeneratedNever();
        builder.Property<Guid>("IdPedido").HasColumnName("id_pedido");
        builder.Property(h => h.De).HasConversion<string>().HasMaxLength(20);
        builder.Property(h => h.Para).HasConversion<string>().HasMaxLength(20);
        builder.Property(h => h.Acao).HasMaxLength(40);
        builder.Property(h => h.Observacao).HasMaxLength(1000);
    }
}

internal sealed class PedidoRepository(FuneralDbContext context) : IPedidoRepository
{
    public Task<Pedido?> ObterAsync(Guid idPedido, CancellationToken cancellationToken = default) =>
        context.Pedidos.FirstOrDefaultAsync(p => p.Id == idPedido, cancellationToken);

    public async Task<IReadOnlyList<Pedido>> ListarDoFuneralAsync(Guid idFuneral, CancellationToken cancellationToken = default) =>
        await context.Pedidos.Where(p => p.IdFuneral == idFuneral).OrderBy(p => p.Id).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Pedido>> ListarDoTerceiroAsync(Guid idTerceiro, StatusPedido? status, CancellationToken cancellationToken = default) =>
        await context.Pedidos
            .AsNoTracking()
            .Where(p => p.IdTerceiro == idTerceiro && (status == null || p.Status == status))
            .OrderBy(p => p.DataHoraEntrega)
            .ToListAsync(cancellationToken);

    public void Adicionar(Pedido pedido) => context.Pedidos.Add(pedido);
}
