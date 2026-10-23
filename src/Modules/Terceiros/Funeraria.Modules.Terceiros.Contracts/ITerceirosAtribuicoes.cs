using Funeraria.BuildingBlocks.Domain.Results;

namespace Funeraria.Modules.Terceiros.Contracts;

/// <summary>
/// Atribuição de serviço a um terceiro pelos módulos de origem: Translado (TRA-04), Documentos (CER-03) e Funeral
/// (FUN-09). O Terceiros guarda a lista única que o parceiro vê no portal (TER-02). Respeita o tenant da execução atual.
/// </summary>
/// <remarks>
/// O resumo é um tipo por serviço, sem campo para CPF, contrato ou dados financeiros: o que o terceiro vê é decidido aqui,
/// na tipagem, e não em cada módulo (LGPD).
/// </remarks>
public interface ITerceirosAtribuicoes
{
    /// <summary>
    /// Atribui o serviço. O terceiro precisa estar ativo (ou ainda convidado) e ser do tipo certo para o serviço.
    /// Idempotente por origem: atribuir de novo a mesma origem ao mesmo terceiro devolve a atribuição existente.
    /// </summary>
    /// <returns>Id da atribuição.</returns>
    Task<Result<Guid>> AtribuirAsync(NovaAtribuicao atribuicao, CancellationToken cancellationToken = default);

    /// <summary>O serviço terminou ou foi cancelado no módulo de origem: sai da lista de pendências do terceiro.</summary>
    Task<Result> EncerrarAsync(Guid idAtribuicao, bool concluida, CancellationToken cancellationToken = default);
}

/// <param name="IdTerceiro">Terceiro que recebe o serviço.</param>
/// <param name="IdOrigem">Registro no módulo de origem (translado, certidão ou pedido).</param>
/// <param name="Resumo">Só os dados que o terceiro pode ver.</param>
/// <param name="PrazoAceite">Até quando ele pode aceitar ou recusar. Sem prazo, vale o padrão da plataforma.</param>
public sealed record NovaAtribuicao(Guid IdTerceiro, Guid IdOrigem, ResumoAtribuicao Resumo, DateTime? PrazoAceite = null);

/// <summary>Dados visíveis ao terceiro. Cada tipo de serviço tem o seu (TER-02, tabela "Dados visíveis por tipo").</summary>
public abstract record ResumoAtribuicao(string NomeFalecido);

/// <summary>Para a FROTA: sem CPF, contrato ou dados financeiros.</summary>
public sealed record ResumoTranslado(
    string NomeFalecido,
    string Origem,
    string Destino,
    DateTime DataHora,
    ContatoResponsavel Contato,
    IReadOnlyList<DocumentoAtribuicao> Documentos)
    : ResumoAtribuicao(NomeFalecido);

/// <summary>Para o CARTORIO_DESPACHANTE: dados e documentos do registro do óbito, sem contrato, valores ou itens do funeral.</summary>
public sealed record ResumoCertidao(
    string NomeFalecido,
    DateOnly DataObito,
    string LocalObito,
    IReadOnlyList<DocumentoAtribuicao> Documentos)
    : ResumoAtribuicao(NomeFalecido);

/// <summary>Para o FORNECEDOR: local, horário e itens do pedido, sem CPF, documentos ou contrato.</summary>
public sealed record ResumoPedido(
    string NomeFalecido,
    string Local,
    DateTime DataHora,
    IReadOnlyList<ItemPedido> Itens)
    : ResumoAtribuicao(NomeFalecido);

public sealed record ContatoResponsavel(string Nome, string Telefone);

/// <param name="Nome">Ex.: "Declaração de óbito".</param>
/// <param name="IdDocumento">Documento no módulo Documentos, baixado por link temporário.</param>
public sealed record DocumentoAtribuicao(string Nome, Guid IdDocumento);

public sealed record ItemPedido(string Nome, int Quantidade, string? Observacao);
