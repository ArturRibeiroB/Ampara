using Funeraria.BuildingBlocks.Domain.Results;

namespace Funeraria.BuildingBlocks.Application.Paginacao;

/// <summary>Ordenação pedida na listagem: <c>?ordenarPor=nome&amp;direcao=asc</c> (INF-17).</summary>
public sealed record OrdenacaoRequest(string? OrdenarPor = null, string? Direcao = null);

/// <summary>
/// Converte a ordenação pedida em <c>ORDER BY</c> seguro. O texto da URL nunca vai para o SQL: ele só escolhe
/// uma entrada da lista de colunas permitidas da consulta. Campo fora da lista vira erro de validação.
/// </summary>
public sealed class OrdenacaoPermitida
{
    private readonly IReadOnlyDictionary<string, string> _colunas;
    private readonly string _padrao;
    private readonly string _desempate;

    /// <param name="colunas">Nome aceito na URL → expressão SQL (ex.: "nome" → "c.nome").</param>
    /// <param name="padrao">Nome aceito usado quando a URL não informa a ordenação.</param>
    /// <param name="desempate">Coluna única que garante ordem estável entre páginas (ex.: o id).</param>
    public OrdenacaoPermitida(IReadOnlyDictionary<string, string> colunas, string padrao, string desempate)
    {
        if (!colunas.ContainsKey(padrao))
        {
            throw new ArgumentException($"A ordenação padrão '{padrao}' não está entre as colunas permitidas.", nameof(padrao));
        }

        _colunas = new Dictionary<string, string>(colunas, StringComparer.OrdinalIgnoreCase);
        _padrao = padrao;
        _desempate = desempate;
    }

    public IEnumerable<string> Campos => _colunas.Keys;

    /// <summary>Cláusula <c>ORDER BY</c> pronta para Dapper, ou erro de validação com os campos aceitos.</summary>
    public Result<string> OrderBy(OrdenacaoRequest? pedido)
    {
        string campo = string.IsNullOrWhiteSpace(pedido?.OrdenarPor) ? _padrao : pedido.OrdenarPor.Trim();
        if (!_colunas.TryGetValue(campo, out string? coluna))
        {
            return Erro("ordenarPor", $"Campo de ordenação inválido. Use: {string.Join(", ", _colunas.Keys)}.");
        }

        string? direcao = pedido?.Direcao?.Trim().ToLowerInvariant();
        string sql = direcao switch
        {
            null or "" or "asc" => "ASC",
            "desc" => "DESC",
            _ => string.Empty
        };

        if (sql.Length == 0)
        {
            return Erro("direcao", "Direção de ordenação inválida. Use asc ou desc.");
        }

        return $"ORDER BY {coluna} {sql}, {_desempate} {sql}";
    }

    private static Result<string> Erro(string campo, string mensagem) =>
        Result.Failure<string>(new ValidationError(new Dictionary<string, string[]> { [campo] = [mensagem] }));
}
