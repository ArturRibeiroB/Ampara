using Funeraria.BuildingBlocks.Domain.Entities;
using Funeraria.BuildingBlocks.Domain.Estados;
using Funeraria.BuildingBlocks.Domain.Events;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.BuildingBlocks.Domain.Tenancy;
using Funeraria.Modules.Funeral.Domain.Beneficiarios;
using Funeraria.Modules.Funeral.Domain.Preferencias;

namespace Funeraria.Modules.Funeral.Domain.Funerais;

/// <summary>
/// Atendimento funeral de um beneficiário (card Registrar Funeral). Começa REGISTRADO e segue a máquina do FUN-06, com
/// histórico de cada mudança. Local, itens, pedidos e translado ficam nos cards FUN-03, FUN-05, FUN-09 e TRA-01 (AJU-01).
/// </summary>
public sealed class Funeral : AgregadoComStatus<StatusFuneral, AcaoFuneral>, ITenantEntity, IAuditableEntity
{
    private Funeral(Guid id, Guid tenantId)
        : base(id, StatusFuneral.Registrado)
    {
        TenantId = tenantId;
    }

    private Funeral()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid IdBeneficiario { get; private set; }

    public Guid IdContrato { get; private set; }

    /// <summary>Titular do contrato, que acompanha o funeral (FUN-07).</summary>
    public Guid IdCliente { get; private set; }

    /// <summary>Copiado do beneficiário para as listagens e para o que os terceiros veem (TER-02).</summary>
    public string NomeFalecido { get; private set; } = null!;

    /// <summary>Data e hora previstas da cerimônia (UTC).</summary>
    public DateTime DataHoraPrevista { get; private set; }

    public Destinacao Destinacao { get; private set; }

    public string? Observacoes { get; private set; }

    protected override MaquinaDeEstados<StatusFuneral, AcaoFuneral> Maquina => EstadosFuneral.Maquina;

    /// <summary>Encerrado: concluído ou cancelado. Itens, salas e pedidos não mudam mais (FUN-03).</summary>
    public bool Encerrado => Status is StatusFuneral.Concluido or StatusFuneral.Cancelado;

    /// <summary>A data do funeral não pode ser anterior ao falecimento (que já não pode ser anterior ao nascimento).</summary>
    public static Result<Funeral> Registrar(
        Beneficiario beneficiario,
        DateTime dataHoraPrevista,
        Destinacao destinacao,
        string? observacoes,
        DateTime agora,
        Guid? idUsuario)
    {
        if (DateOnly.FromDateTime(dataHoraPrevista) < beneficiario.DataFalecimento)
        {
            return FuneralErros.AntesDoFalecimento;
        }

        var funeral = new Funeral(Guid.CreateVersion7(), beneficiario.TenantId)
        {
            IdBeneficiario = beneficiario.Id,
            IdContrato = beneficiario.IdContrato,
            IdCliente = beneficiario.IdCliente,
            NomeFalecido = beneficiario.Nome,
            DataHoraPrevista = dataHoraPrevista,
            Destinacao = destinacao,
            Observacoes = string.IsNullOrWhiteSpace(observacoes) ? null : observacoes.Trim()
        };

        funeral.RegistrarInicio(agora, idUsuario);
        funeral.Raise(new FuneralRegistradoDomainEvent(funeral.Id));
        return funeral;
    }

    /// <summary>Muda para o status pedido, se a transição for permitida (FUN-06).</summary>
    public Result MudarPara(StatusFuneral destino, DateTime agora, Guid? idUsuario, string? observacao) =>
        EstadosFuneral.AcaoPara(destino) is AcaoFuneral acao
            ? Executar(acao, agora, idUsuario, observacao)
            : FuneralErros.TransicaoInvalida(Status, destino);

    protected override void AoMudarStatus(StatusFuneral anterior, StatusFuneral atual, AcaoFuneral acao) =>
        Raise(new FuneralStatusAlteradoDomainEvent(Id, anterior, atual));
}

public sealed record FuneralRegistradoDomainEvent(Guid IdFuneral) : DomainEvent;

public sealed record FuneralStatusAlteradoDomainEvent(Guid IdFuneral, StatusFuneral Anterior, StatusFuneral Atual) : DomainEvent;

public static class FuneralErros
{
    public static readonly Error NaoEncontrado =
        Error.NaoEncontrado("Funeral.NaoEncontrado", "Funeral não encontrado.");

    public static readonly Error JaRegistrado =
        Error.Conflito("Funeral.JaRegistrado", "Este beneficiário já tem um funeral registrado.");

    public static readonly Error AntesDoFalecimento =
        Error.Validacao("Funeral.AntesDoFalecimento", "A data do funeral não pode ser anterior à data de falecimento.");

    public static readonly Error Encerrado =
        Error.Conflito("Funeral.Encerrado", "Funeral concluído ou cancelado não pode ser alterado.");

    public static Error TransicaoInvalida(StatusFuneral atual, StatusFuneral destino) =>
        Error.Validacao("Estado.TransicaoInvalida",
            $"Não é possível mudar de {NomeApi.De(atual)} para {NomeApi.De(destino)}.");
}

public interface IFuneralRepository
{
    Task<Funeral?> ObterAsync(Guid idFuneral, CancellationToken cancellationToken = default);

    /// <summary>Funeral não cancelado do beneficiário.</summary>
    Task<bool> ExisteAtivoDoBeneficiarioAsync(Guid idBeneficiario, CancellationToken cancellationToken = default);

    /// <summary>Funeral do contrato ainda não concluído nem cancelado (CON-03).</summary>
    Task<bool> ExisteEmAndamentoNoContratoAsync(Guid idContrato, CancellationToken cancellationToken = default);

    void Adicionar(Funeral funeral);
}
