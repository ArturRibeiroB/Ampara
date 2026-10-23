using Funeraria.BuildingBlocks.Domain.Entities;
using Funeraria.BuildingBlocks.Domain.Events;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.BuildingBlocks.Domain.Tenancy;

namespace Funeraria.Modules.Terceiros.Domain.Terceiros;

/// <summary>
/// Parceiro da funerária (TER-01, DEC-06): empresa de translado, despachante/cartório ou fornecedor. A conta de acesso é
/// única na plataforma (AUT-01) e fica na Identidade; aqui ficam os dados do parceiro nesta funerária.
/// </summary>
public sealed class Terceiro : AggregateRoot<Guid>, ITenantEntity, IAuditableEntity
{
    private Terceiro(Guid id, Guid tenantId)
        : base(id)
    {
        TenantId = tenantId;
    }

    private Terceiro()
    {
    }

    public Guid TenantId { get; private set; }

    public TipoTerceiro Tipo { get; private set; }

    public string NomeRazaoSocial { get; private set; } = null!;

    /// <summary>Só dígitos (ou letras e dígitos, no CNPJ alfanumérico). Único dentro da funerária.</summary>
    public string CpfCnpj { get; private set; } = null!;

    public string? Telefone { get; private set; }

    /// <summary>E-mail da conta de acesso. Não muda depois do convite.</summary>
    public string Email { get; private set; } = null!;

    public AreaAtuacao AreaAtuacao { get; private set; } = new();

    /// <summary>Categorias do catálogo atendidas (só para FORNECEDOR, FUN-09).</summary>
    public IReadOnlyList<Guid> CategoriasAtendidas { get; private set; } = [];

    public StatusTerceiro Status { get; private set; }

    /// <summary>Conta na Identidade, preenchida quando o vínculo é criado.</summary>
    public Guid? IdUsuario { get; private set; }

    /// <summary>A conta já tem senha: o convite foi aceito ou a pessoa já usava a plataforma.</summary>
    public bool ContaAtiva { get; private set; }

    public static Terceiro Cadastrar(Guid tenantId, TipoTerceiro tipo, string email, DadosTerceiro dados)
    {
        var terceiro = new Terceiro(Guid.CreateVersion7(), tenantId)
        {
            Tipo = tipo,
            Email = email.Trim(),
            Status = StatusTerceiro.Convidado
        };

        terceiro.Atualizar(dados);
        terceiro.Raise(new TerceiroCadastradoDomainEvent(terceiro.Id));
        return terceiro;
    }

    public void Atualizar(DadosTerceiro dados)
    {
        NomeRazaoSocial = dados.NomeRazaoSocial.Trim();
        CpfCnpj = dados.CpfCnpj;
        Telefone = string.IsNullOrWhiteSpace(dados.Telefone) ? null : dados.Telefone.Trim();
        AreaAtuacao = dados.AreaAtuacao;
        CategoriasAtendidas = Tipo == TipoTerceiro.Fornecedor ? [.. dados.CategoriasAtendidas.Distinct()] : [];
    }

    /// <summary>A Identidade criou o vínculo com a conta (TER-01).</summary>
    public void VincularConta(Guid idUsuario, bool contaAtiva)
    {
        IdUsuario = idUsuario;
        if (contaAtiva)
        {
            AtivarConta();
        }
    }

    /// <summary>O parceiro definiu a senha: deixa de ser só convidado.</summary>
    public void AtivarConta()
    {
        ContaAtiva = true;
        if (Status == StatusTerceiro.Convidado)
        {
            Status = StatusTerceiro.Ativo;
        }
    }

    /// <summary>Desativar não apaga o histórico e tira o acesso a esta funerária; inativo não recebe atribuições.</summary>
    public void AlterarStatus(bool ativo)
    {
        StatusTerceiro novo = !ativo ? StatusTerceiro.Inativo : ContaAtiva ? StatusTerceiro.Ativo : StatusTerceiro.Convidado;
        if (novo == Status)
        {
            return;
        }

        Status = novo;
        Raise(new TerceiroStatusAlteradoDomainEvent(Id, ativo));
    }
}

public enum TipoTerceiro
{
    /// <summary>Empresa de translado.</summary>
    Frota = 1,

    /// <summary>Despachante ou cartório parceiro.</summary>
    CartorioDespachante = 2,

    /// <summary>Floricultura, buffet, cerimonialista.</summary>
    Fornecedor = 3
}

public enum StatusTerceiro
{
    Convidado = 1,
    Ativo = 2,
    Inativo = 3
}

/// <summary>Onde o parceiro atende: UFs inteiras e/ou cidades. Gravada em jsonb.</summary>
public sealed record AreaAtuacao
{
    public IReadOnlyList<string> Ufs { get; init; } = [];

    public IReadOnlyList<CidadeAtendida> Cidades { get; init; } = [];

    public bool Equals(AreaAtuacao? other) =>
        other is not null && Ufs.SequenceEqual(other.Ufs) && Cidades.SequenceEqual(other.Cidades);

    public override int GetHashCode() => HashCode.Combine(Ufs.Count, Cidades.Count);
}

public sealed record CidadeAtendida(string Nome, string Uf);

public sealed record DadosTerceiro(
    string NomeRazaoSocial,
    string CpfCnpj,
    string? Telefone,
    AreaAtuacao AreaAtuacao,
    IReadOnlyList<Guid> CategoriasAtendidas);

public sealed record TerceiroCadastradoDomainEvent(Guid IdTerceiro) : DomainEvent;

public sealed record TerceiroStatusAlteradoDomainEvent(Guid IdTerceiro, bool Ativo) : DomainEvent;

public static class TerceiroErros
{
    public static readonly Error NaoEncontrado =
        Error.NaoEncontrado("Terceiro.NaoEncontrado", "Terceiro não encontrado.");

    public static readonly Error CpfCnpjDuplicado =
        Error.Conflito("Terceiro.CpfCnpjDuplicado", "CPF ou CNPJ já cadastrado nesta funerária.");

    public static readonly Error EmailDuplicado =
        Error.Conflito("Terceiro.EmailDuplicado", "Já existe um terceiro com este e-mail nesta funerária.");

    public static readonly Error EmailComAcesso =
        Error.Conflito("Terceiro.EmailComAcesso", "Este e-mail já tem acesso a esta funerária com outro perfil.");
}

public interface ITerceiroRepository
{
    Task<Terceiro?> ObterAsync(Guid idTerceiro, CancellationToken cancellationToken = default);

    Task<bool> ExisteCpfCnpjAsync(string cpfCnpj, Guid? exceto, CancellationToken cancellationToken = default);

    Task<bool> ExisteEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Terceiros da conta em todas as funerárias (a conta é da plataforma).</summary>
    Task<IReadOnlyList<Terceiro>> ListarDaContaEmTodasAsFunerariasAsync(Guid idUsuario, CancellationToken cancellationToken = default);

    void Adicionar(Terceiro terceiro);
}
