using Funeraria.BuildingBlocks.Domain.Entities;
using Funeraria.BuildingBlocks.Domain.Events;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.BuildingBlocks.Domain.Tenancy;
using Funeraria.Modules.Terceiros.Domain.Terceiros;

namespace Funeraria.Modules.Terceiros.Domain.Atribuicoes;

/// <summary>
/// Serviço atribuído a um terceiro: translado, certidão ou pedido (TER-02). O registro do serviço fica no módulo de origem;
/// aqui fica a lista única que o parceiro vê, com só os dados que ele pode ver (resumo).
/// </summary>
public sealed class Atribuicao : AggregateRoot<Guid>, ITenantEntity, IAuditableEntity
{
    /// <summary>Prazo para aceitar ou recusar quando o módulo de origem não informa um.</summary>
    public static readonly TimeSpan PrazoPadrao = TimeSpan.FromHours(24);

    private Atribuicao(Guid id, Guid tenantId)
        : base(id)
    {
        TenantId = tenantId;
    }

    private Atribuicao()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid IdTerceiro { get; private set; }

    public TipoAtribuicao Tipo { get; private set; }

    /// <summary>Registro no módulo de origem.</summary>
    public Guid IdOrigem { get; private set; }

    /// <summary>Dados visíveis ao terceiro, em JSON (jsonb).</summary>
    public string Resumo { get; private set; } = null!;

    public StatusAtribuicao Status { get; private set; }

    public DateTime PrazoAceite { get; private set; }

    public DateTime? RespondidaEm { get; private set; }

    public string? MotivoRecusa { get; private set; }

    public static Atribuicao Criar(Guid tenantId, Guid idTerceiro, TipoAtribuicao tipo, Guid idOrigem, string resumo, DateTime prazoAceite)
    {
        var atribuicao = new Atribuicao(Guid.CreateVersion7(), tenantId)
        {
            IdTerceiro = idTerceiro,
            Tipo = tipo,
            IdOrigem = idOrigem,
            Resumo = resumo,
            Status = StatusAtribuicao.Pendente,
            PrazoAceite = prazoAceite
        };
        atribuicao.Raise(new AtribuicaoCriadaDomainEvent(atribuicao.Id));
        return atribuicao;
    }

    /// <summary>Status para mostrar: pendente com o prazo vencido aparece como EXPIRADA.</summary>
    public StatusAtribuicao StatusEm(DateTime agora) =>
        Status == StatusAtribuicao.Pendente && agora > PrazoAceite ? StatusAtribuicao.Expirada : Status;

    public Result Aceitar(DateTime agora) => Responder(agora, StatusAtribuicao.Aceita, motivo: null);

    public Result Recusar(DateTime agora, string motivo) => Responder(agora, StatusAtribuicao.Recusada, motivo.Trim());

    /// <summary>O módulo de origem concluiu ou cancelou o serviço.</summary>
    public void Encerrar(bool concluida) => Status = concluida ? StatusAtribuicao.Concluida : StatusAtribuicao.Cancelada;

    /// <summary>Só o tipo de terceiro certo recebe cada serviço (DEC-06).</summary>
    public static TipoTerceiro TerceiroDoTipo(TipoAtribuicao tipo) => tipo switch
    {
        TipoAtribuicao.Translado => TipoTerceiro.Frota,
        TipoAtribuicao.Certidao => TipoTerceiro.CartorioDespachante,
        _ => TipoTerceiro.Fornecedor
    };

    private Result Responder(DateTime agora, StatusAtribuicao resposta, string? motivo)
    {
        if (Status != StatusAtribuicao.Pendente)
        {
            return AtribuicaoErros.JaRespondida;
        }

        if (agora > PrazoAceite)
        {
            return AtribuicaoErros.PrazoEncerrado;
        }

        Status = resposta;
        RespondidaEm = agora;
        MotivoRecusa = motivo;
        Raise(new AtribuicaoRespondidaDomainEvent(Id, resposta == StatusAtribuicao.Aceita));
        return Result.Success();
    }
}

public enum TipoAtribuicao
{
    Translado = 1,
    Certidao = 2,
    Pedido = 3
}

public enum StatusAtribuicao
{
    Pendente = 1,
    Aceita = 2,
    Recusada = 3,

    /// <summary>Calculado: pendente com o prazo vencido. Não é gravado.</summary>
    Expirada = 4,
    Concluida = 5,
    Cancelada = 6
}

public sealed record AtribuicaoCriadaDomainEvent(Guid IdAtribuicao) : DomainEvent;

public sealed record AtribuicaoRespondidaDomainEvent(Guid IdAtribuicao, bool Aceita) : DomainEvent;

public static class AtribuicaoErros
{
    /// <summary>Também para atribuição de outro terceiro: não revela que ela existe (TER-02).</summary>
    public static readonly Error NaoEncontrada =
        Error.NaoEncontrado("Atribuicao.NaoEncontrada", "Atribuição não encontrada.");

    public static readonly Error JaRespondida =
        Error.Conflito("Atribuicao.JaRespondida", "Esta atribuição já foi respondida.");

    public static readonly Error PrazoEncerrado =
        Error.Conflito("Atribuicao.PrazoEncerrado", "O prazo para aceitar ou recusar terminou. Fale com a funerária.");

    public static readonly Error TerceiroIndisponivel =
        Error.Conflito("Atribuicao.TerceiroIndisponivel", "Terceiro inativo não recebe novas atribuições.");

    public static readonly Error TipoIncompativel =
        Error.Validacao("Atribuicao.TipoIncompativel", "Este terceiro não presta este tipo de serviço.");
}

public interface IAtribuicaoRepository
{
    Task<Atribuicao?> ObterAsync(Guid idAtribuicao, CancellationToken cancellationToken = default);

    Task<Atribuicao?> ObterPorOrigemAsync(Guid idTerceiro, TipoAtribuicao tipo, Guid idOrigem, CancellationToken cancellationToken = default);

    /// <summary>Atribuições de um terceiro, das mais recentes para as mais antigas.</summary>
    Task<IReadOnlyList<Atribuicao>> ListarDoTerceiroAsync(Guid idTerceiro, CancellationToken cancellationToken = default);

    void Adicionar(Atribuicao atribuicao);
}
