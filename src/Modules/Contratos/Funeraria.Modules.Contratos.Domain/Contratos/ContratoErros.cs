using Funeraria.BuildingBlocks.Domain.Results;

namespace Funeraria.Modules.Contratos.Domain.Contratos;

/// <summary>Erros do card Criar Contrato, com as mensagens do card.</summary>
public static class ContratoErros
{
    public const string TitularObrigatorio = "Cliente titular é obrigatório.";
    public const string PlanoObrigatorio = "Plano é obrigatório.";
    public const string TipoCoberturaObrigatorio = "Tipo de cobertura é obrigatório.";
    public const string DependenteInvalido = "Dependente selecionado é inválido.";

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

    /// <summary>Multa, juros e desconto anual ainda não preenchidos pela funerária (TEN-03, DEC-12).</summary>
    public static readonly Error ContratacaoIndisponivel =
        Error.Conflito("Contrato.ContratacaoIndisponivel",
            "A contratação ainda não está disponível: a funerária precisa configurar multa, juros e desconto.");
}

public interface IContratoRepository
{
    Task<Contrato?> ObterAsync(Guid idContrato, CancellationToken cancellationToken = default);

    /// <summary>O cliente tem contrato não cancelado neste plano.</summary>
    Task<bool> ExisteVigenteAsync(Guid idCliente, Guid idPlano, CancellationToken cancellationToken = default);

    /// <summary>Dos dependentes informados, os que já estão cobertos por um contrato não cancelado.</summary>
    Task<IReadOnlyList<Guid>> DependentesCobertosAsync(IReadOnlyCollection<Guid> idsDependentes, CancellationToken cancellationToken = default);

    void Adicionar(Contrato contrato);
}
