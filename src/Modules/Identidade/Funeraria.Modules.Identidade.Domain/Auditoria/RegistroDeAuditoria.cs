using Funeraria.BuildingBlocks.Domain.Entities;

namespace Funeraria.Modules.Identidade.Domain.Auditoria;

/// <summary>
/// Linha da auditoria (AUT-09): quem fez o quê, onde e quando. Só é inserida, nunca alterada.
/// Sem filtro global de tenant: a consulta filtra a funerária explicitamente, e ações da plataforma não têm funerária.
/// </summary>
public sealed class RegistroDeAuditoria : Entity<Guid>
{
    private RegistroDeAuditoria(Guid id)
        : base(id)
    {
    }

    private RegistroDeAuditoria()
    {
    }

    public Guid? IdTenant { get; private set; }

    /// <summary>Feita fora de uma funerária (pelo ADMIN, no endereço da plataforma). É o que o ADMIN consulta (DEC-06).</summary>
    public bool Plataforma { get; private set; }

    public Guid? IdUsuario { get; private set; }

    public string Acao { get; private set; } = null!;

    public string Entidade { get; private set; } = null!;

    public Guid? IdEntidade { get; private set; }

    /// <summary>JSON (jsonb).</summary>
    public string? ValoresAnteriores { get; private set; }

    /// <summary>JSON (jsonb).</summary>
    public string? ValoresNovos { get; private set; }

    public DateTime DataHora { get; private set; }

    public string? Ip { get; private set; }

    public static RegistroDeAuditoria Criar(
        Guid? idTenant,
        bool plataforma,
        Guid? idUsuario,
        string acao,
        string entidade,
        Guid? idEntidade,
        string? valoresAnteriores,
        string? valoresNovos,
        DateTime dataHora,
        string? ip) =>
        new(Guid.CreateVersion7())
        {
            IdTenant = idTenant,
            Plataforma = plataforma,
            IdUsuario = idUsuario,
            Acao = acao,
            Entidade = entidade,
            IdEntidade = idEntidade,
            ValoresAnteriores = valoresAnteriores,
            ValoresNovos = valoresNovos,
            DataHora = dataHora,
            Ip = ip
        };
}
