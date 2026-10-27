using System.Text.Json;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Serialization;
using Funeraria.Modules.Identidade.Domain.Auditoria;
using Funeraria.Modules.Identidade.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Identidade.Infrastructure.Auditoria;

/// <summary>
/// Grava a auditoria na hora, num contexto próprio: o registro não depende da transação do módulo que chamou
/// (que já gravou a operação antes de auditar).
/// </summary>
internal sealed class AuditoriaService(
    IServiceScopeFactory escopos,
    ICurrentUser usuario,
    ITenantContext tenantContext,
    IOrigemRequisicao origem,
    TimeProvider timeProvider)
    : IAuditoria
{
    public async Task RegistrarAsync(RegistroAuditoria registro, CancellationToken cancellationToken = default)
    {
        var linha = RegistroDeAuditoria.Criar(
            registro.IdTenant ?? tenantContext.TenantId,
            plataforma: tenantContext.TenantId is null,
            usuario.IdUsuario,
            registro.Acao,
            registro.Entidade,
            registro.IdEntidade,
            Json(registro.ValoresAnteriores),
            Json(registro.ValoresNovos),
            timeProvider.GetUtcNow().UtcDateTime,
            origem.Ip);

        await using AsyncServiceScope escopo = escopos.CreateAsyncScope();
        var context = escopo.ServiceProvider.GetRequiredService<IdentidadeDbContext>();
        context.Set<RegistroDeAuditoria>().Add(linha);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static string? Json(object? valores) =>
        valores is null ? null : JsonSerializer.Serialize(valores, valores.GetType(), JsonbOptions.Default);
}

internal sealed class RegistroDeAuditoriaConfiguration : IEntityTypeConfiguration<RegistroDeAuditoria>
{
    public void Configure(EntityTypeBuilder<RegistroDeAuditoria> builder)
    {
        builder.ToTable("auditoria");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id_auditoria").ValueGeneratedNever();
        builder.Property(a => a.IdTenant).HasColumnName("id_tenant");
        builder.Property(a => a.Acao).HasMaxLength(60);
        builder.Property(a => a.Entidade).HasMaxLength(60);
        builder.Property(a => a.ValoresAnteriores).HasColumnType("jsonb");
        builder.Property(a => a.ValoresNovos).HasColumnType("jsonb");
        builder.Property(a => a.Ip).HasMaxLength(45);

        builder.HasIndex(a => new { a.IdTenant, a.DataHora });
        builder.HasIndex(a => new { a.Entidade, a.IdEntidade });
    }
}
