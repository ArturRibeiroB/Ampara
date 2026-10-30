using Funeraria.BuildingBlocks.Domain.Results;

namespace Funeraria.Modules.Contratos.Domain.Contratos;

/// <summary>Erros do card Criar Contrato, com as mensagens do card.</summary>
public static class ContratoErros
{
    public const string TitularObrigatorio = "Cliente titular é obrigatório.";
    public const string PlanoObrigatorio = "Plano é obrigatório.";
    public const string TipoCoberturaObrigatorio = "Tipo de cobertura é obrigatório.";
    public const string DependenteInvalido = "Dependente selecionado é inválido.";

    /// <summary>Também para o cliente que pede o contrato de outro: não revela que ele existe (CON-01).</summary>
    public static readonly Error NaoEncontrado =
        Error.NaoEncontrado("Contrato.NaoEncontrado", "Contrato não encontrado.");

    public static readonly Error TitularNaoEncontrado =
        Error.NaoEncontrado("Contrato.TitularNaoEncontrado", "Cliente titular não encontrado.");

    public static readonly Error PlanoNaoEncontrado =
        Error.NaoEncontrado("Contrato.PlanoNaoEncontrado", "Plano não encontrado.");

    public static readonly Error DependenteNaoEncontrado =
        Error.NaoEncontrado("Contrato.DependenteNaoEncontrado", "Dependente não encontrado.");

    public static readonly Error DependenteDeOutroCliente =
        Error.Validacao("Contrato.DependenteDeOutroCliente", "Dependente não pertence ao cliente informado.");

    public static readonly Error LimiteDependentes =
        Error.Validacao("Contrato.LimiteDependentes", "Quantidade de dependentes excede o limite do plano.");

    public static readonly Error SomenteDependentesNaoPermitido =
        Error.Validacao("Contrato.SomenteDependentesNaoPermitido", "Este plano não permite contratação somente para dependentes.");

    public static Error DiaVencimentoNaoPermitido(IEnumerable<int> permitidos) =>
        Error.Validacao("Contrato.DiaVencimento", $"Dia de vencimento deve ser um destes: {string.Join(", ", permitidos)}.");

    public static readonly Error ContratoVigenteNoPlano =
        Error.Conflito("Contrato.ContratoVigenteNoPlano", "Cliente já possui um contrato ativo para este plano.");

    public static readonly Error DependenteEmContratoVigente =
        Error.Conflito("Contrato.DependenteEmContratoVigente", "Dependente já está vinculado a um contrato ativo.");

    public static readonly Error ContratoCancelado =
        Error.Conflito("Contrato.Cancelado", "Este contrato está cancelado.");

    public static readonly Error DependenteNaoCoberto =
        Error.NaoEncontrado("Contrato.DependenteNaoCoberto", "Dependente não está coberto por este contrato.");

    public static readonly Error UltimoDependente =
        Error.Conflito("Contrato.UltimoDependente",
            "O contrato cobre somente dependentes e precisa manter ao menos um. Para encerrar, cancele o contrato.");

    public static readonly Error DependenteBeneficiario =
        Error.Conflito("Contrato.DependenteBeneficiario", "Dependente já registrado como beneficiário não pode ser removido.");

    public static readonly Error FuneralEmAndamento =
        Error.Conflito("Contrato.FuneralEmAndamento", "Contrato com funeral em andamento não pode ser cancelado.");

    /// <summary>Cancelamento é do titular ou da equipe interna (gestor e funcionário), não do consultor (CON-03).</summary>
    public static readonly Error CancelamentoNaoPermitido =
        Error.Proibido("Contrato.CancelamentoNaoPermitido", "Somente o titular ou a equipe da funerária podem cancelar o contrato.");

    /// <summary>Multa, juros e desconto anual ainda não preenchidos pela funerária (TEN-03, DEC-12).</summary>
    public static readonly Error ContratacaoIndisponivel =
        Error.Conflito("Contrato.ContratacaoIndisponivel",
            "A contratação ainda não está disponível: a funerária precisa configurar multa, juros e desconto.");
}

public interface IContratoRepository
{
    Task<Contrato?> ObterAsync(Guid idContrato, CancellationToken cancellationToken = default);

    /// <summary>Contratos do cliente, do mais recente para o mais antigo, com as pessoas cobertas.</summary>
    Task<IReadOnlyList<Contrato>> ListarDoClienteAsync(Guid idCliente, CancellationToken cancellationToken = default);

    /// <summary>O cliente tem contrato não cancelado neste plano.</summary>
    Task<bool> ExisteVigenteAsync(Guid idCliente, Guid idPlano, CancellationToken cancellationToken = default);

    /// <summary>Dos dependentes informados, os que já estão cobertos por um contrato não cancelado.</summary>
    Task<IReadOnlyList<Guid>> DependentesCobertosAsync(IReadOnlyCollection<Guid> idsDependentes, CancellationToken cancellationToken = default);

    /// <summary>Titulares com contrato no status informado (qualquer status se nulo).</summary>
    Task<IReadOnlyList<Guid>> ClientesComContratoAsync(StatusContrato? status, CancellationToken cancellationToken = default);

    /// <summary>Contrato que tem a pessoa coberta informada.</summary>
    Task<Contrato?> ObterPorPessoaCobertaAsync(Guid idPessoaCoberta, CancellationToken cancellationToken = default);

    void Adicionar(Contrato contrato);
}
