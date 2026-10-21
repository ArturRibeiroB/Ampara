namespace Funeraria.BuildingBlocks.Application.Paginacao;

/// <summary>Parâmetros de listagem: <c>?pagina=1&amp;tamanho=20</c> (INF-17).</summary>
public sealed record PaginaRequest(int Pagina = 1, int Tamanho = PaginaRequest.TamanhoPadrao)
{
    public const int TamanhoPadrao = 20;
    public const int TamanhoMaximo = 100;

    /// <summary>Página a partir de 1 e tamanho entre 1 e <see cref="TamanhoMaximo"/>, sem rejeitar a requisição.</summary>
    public PaginaRequest Normalizada() => new(Math.Max(1, Pagina), Math.Clamp(Tamanho, 1, TamanhoMaximo));

    public int Deslocamento => (Pagina - 1) * Tamanho;
}

/// <summary>Resposta paginada: <c>{ itens, pagina, tamanho, total }</c> (INF-17).</summary>
public sealed record PaginaResult<T>(IReadOnlyList<T> Itens, int Pagina, int Tamanho, long Total);
