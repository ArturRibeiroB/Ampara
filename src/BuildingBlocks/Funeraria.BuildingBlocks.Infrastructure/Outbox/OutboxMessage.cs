using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Funeraria.BuildingBlocks.Infrastructure.Outbox;

/// <summary>Evento de domínio aguardando despacho. Cada módulo tem a sua tabela <c>{schema}.outbox_messages</c>.</summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; }

    /// <summary>Nome do tipo do evento no formato "Namespace.Tipo, Assembly".</summary>
    public required string Type { get; init; }

    public required string Content { get; init; }

    public Guid? TenantId { get; init; }

    public DateTime OccurredOnUtc { get; init; }

    public DateTime? ProcessedOnUtc { get; set; }

    public int Attempts { get; set; }

    public string? Error { get; set; }

    internal static string TypeName(Type type) => $"{type.FullName}, {type.Assembly.GetName().Name}";
}

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Type).HasMaxLength(300);
        builder.Property(m => m.Content).HasColumnType("jsonb");
        builder.Property(m => m.TenantId).HasColumnName("id_tenant");
        builder.Property(m => m.Error).HasMaxLength(4000);
        builder.HasIndex(m => m.OccurredOnUtc).HasFilter("processed_on_utc IS NULL");
    }
}
