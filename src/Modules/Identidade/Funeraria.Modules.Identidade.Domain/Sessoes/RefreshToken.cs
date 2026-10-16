using Funeraria.BuildingBlocks.Domain.Entities;

namespace Funeraria.Modules.Identidade.Domain.Sessoes;

/// <summary>
/// Refresh token de uma sessão (AUT-03). Só o hash é gravado. Cada uso gera um token novo (rotação) e
/// revoga o anterior; apresentar de novo um token já trocado indica roubo e derruba todas as sessões do usuário.
/// </summary>
public sealed class RefreshToken : AggregateRoot<Guid>
{
    private RefreshToken(Guid id)
        : base(id)
    {
    }

    private RefreshToken()
    {
    }

    public Guid IdUsuario { get; private set; }

    /// <summary>Funerária da sessão. Nulo na sessão do ADMIN, que é da plataforma.</summary>
    public Guid? IdTenant { get; private set; }

    public string TokenHash { get; private set; } = null!;

    public DateTime CriadoEm { get; private set; }

    public DateTime ExpiraEm { get; private set; }

    public DateTime? RevogadoEm { get; private set; }

    /// <summary>Token que substituiu este na rotação. Preenchido = já foi usado.</summary>
    public Guid? SubstituidoPor { get; private set; }

    public bool Revogado => RevogadoEm is not null;

    public bool FoiRotacionado => SubstituidoPor is not null;

    public static RefreshToken Criar(Guid idUsuario, Guid? idTenant, string tokenHash, DateTime agora, TimeSpan duracao) =>
        new(Guid.CreateVersion7())
        {
            IdUsuario = idUsuario,
            IdTenant = idTenant,
            TokenHash = tokenHash,
            CriadoEm = agora,
            ExpiraEm = agora + duracao
        };

    public bool EstaAtivo(DateTime agora) => !Revogado && agora < ExpiraEm;

    /// <summary>Revoga este token e devolve o que o substitui, na mesma sessão (mesmo usuário e funerária).</summary>
    public RefreshToken Rotacionar(string novoTokenHash, DateTime agora, TimeSpan duracao)
    {
        if (!EstaAtivo(agora))
        {
            throw new InvalidOperationException("Só um refresh token ativo pode ser rotacionado.");
        }

        RefreshToken novo = Criar(IdUsuario, IdTenant, novoTokenHash, agora, duracao);
        RevogadoEm = agora;
        SubstituidoPor = novo.Id;
        return novo;
    }

    public void Revogar(DateTime agora) => RevogadoEm ??= agora;
}
