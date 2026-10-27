using Funeraria.BuildingBlocks.Domain.Entities;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.BuildingBlocks.Domain.Tenancy;

namespace Funeraria.Modules.Funeral.Domain.Preferencias;

/// <summary>
/// Como o titular deseja o próprio funeral ou o de um dependente, registrado em vida (FUN-02, DEC-07). Serve de base
/// quando o funeral for montado (FUN-03). Crença religiosa é dado sensível (LGPD): fica cifrada no banco.
/// </summary>
public sealed class PreferenciaFuneral : AggregateRoot<Guid>, ITenantEntity, IAuditableEntity
{
    private PreferenciaFuneral(Guid id, Guid tenantId)
        : base(id)
    {
        TenantId = tenantId;
    }

    private PreferenciaFuneral()
    {
    }

    public Guid TenantId { get; private set; }

    /// <summary>Titular dono do registro.</summary>
    public Guid IdCliente { get; private set; }

    /// <summary>A pessoa: o próprio titular (id do cliente) ou um dependente (id do dependente).</summary>
    public Guid IdPessoa { get; private set; }

    public TipoPessoa TipoPessoa { get; private set; }

    public Destinacao Destinacao { get; private set; }

    public TipoCerimonia TipoCerimonia { get; private set; }

    /// <summary>Dado sensível (LGPD, art. 5º, II). Cifrado em repouso.</summary>
    public string? CrencaReligiosa { get; private set; }

    public IReadOnlyList<string> Musicas { get; private set; } = [];

    public string? Mensagens { get; private set; }

    public string? Observacoes { get; private set; }

    /// <summary>Itens do catálogo escolhidos (urna, flores...). Gravado em jsonb.</summary>
    public IReadOnlyList<ItemPreferido> Itens { get; private set; } = [];

    public static PreferenciaFuneral Criar(Guid tenantId, Guid idCliente, Guid idPessoa, TipoPessoa tipo, DadosPreferencia dados)
    {
        var preferencia = new PreferenciaFuneral(Guid.CreateVersion7(), tenantId) { IdCliente = idCliente, IdPessoa = idPessoa, TipoPessoa = tipo };
        preferencia.Atualizar(dados);
        return preferencia;
    }

    public void Atualizar(DadosPreferencia dados)
    {
        Destinacao = dados.Destinacao;
        TipoCerimonia = dados.TipoCerimonia;
        CrencaReligiosa = Texto(dados.CrencaReligiosa);
        Musicas = [.. dados.Musicas.Select(m => m.Trim()).Where(m => m.Length > 0)];
        Mensagens = Texto(dados.Mensagens);
        Observacoes = Texto(dados.Observacoes);
        Itens = dados.Itens;
    }

    private static string? Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

public enum TipoPessoa
{
    Titular = 1,
    Dependente = 2
}

public enum Destinacao
{
    NaoDefinida = 1,
    Sepultamento = 2,
    Cremacao = 3
}

public enum TipoCerimonia
{
    NaoDefinida = 1,
    Religiosa = 2,
    Ecumenica = 3,
    Civil = 4,
    SemCerimonia = 5
}

/// <summary>Item do catálogo escolhido, com a variação (ex.: urna em mogno) e a quantidade.</summary>
public sealed record ItemPreferido(Guid IdItemCatalogo, Guid? IdVariacao, int Quantidade);

public sealed record DadosPreferencia(
    Destinacao Destinacao,
    TipoCerimonia TipoCerimonia,
    string? CrencaReligiosa,
    IReadOnlyList<string> Musicas,
    string? Mensagens,
    string? Observacoes,
    IReadOnlyList<ItemPreferido> Itens);

public static class PreferenciaErros
{
    /// <summary>Também para pessoa de outro titular: não revela que ela existe.</summary>
    public static readonly Error PessoaNaoEncontrada =
        Error.NaoEncontrado("Preferencia.PessoaNaoEncontrada", "Pessoa não encontrada entre o titular e os seus dependentes.");

    public static Error ItensInvalidos(IEnumerable<Guid> ids) =>
        new ValidationError(new Dictionary<string, string[]>
        {
            ["itens"] = [$"Item ou variação do catálogo não encontrado ou inativo: {string.Join(", ", ids)}."]
        });
}

public interface IPreferenciaRepository
{
    Task<IReadOnlyList<PreferenciaFuneral>> ListarDoTitularAsync(Guid idCliente, CancellationToken cancellationToken = default);

    Task<PreferenciaFuneral?> ObterDaPessoaAsync(Guid idCliente, Guid idPessoa, CancellationToken cancellationToken = default);

    void Adicionar(PreferenciaFuneral preferencia);
}
