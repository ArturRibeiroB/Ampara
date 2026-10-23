using Funeraria.BuildingBlocks.Domain.Entities;

namespace Funeraria.Modules.Planos.Domain.Planos;

/// <summary>
/// Item do catálogo coberto pelo plano, ex.: urna padrão, 1 coroa de flores, velório de 24h (PLA-02, DEC-07).
/// Guarda só o id do item: nome, preço e status vêm do módulo Catalogo.
/// </summary>
public sealed class PlanoCobertura : Entity<Guid>
{
    private PlanoCobertura(Guid id)
        : base(id)
    {
    }

    private PlanoCobertura()
    {
    }

    public Guid IdItemCatalogo { get; private set; }

    public int Quantidade { get; private set; }

    /// <summary>Quilômetros de translado incluídos, quando o item é de translado.</summary>
    public int? KmTransladoIncluso { get; private set; }

    internal static PlanoCobertura Criar(Guid idItemCatalogo, int quantidade, int? kmTransladoIncluso) =>
        new(Guid.CreateVersion7()) { IdItemCatalogo = idItemCatalogo, Quantidade = quantidade, KmTransladoIncluso = kmTransladoIncluso };
}

public sealed record CoberturaDefinida(Guid IdItemCatalogo, int Quantidade, int? KmTransladoIncluso);
