using Funeraria.BuildingBlocks.Domain.Entities;

namespace Funeraria.Modules.Identidade.Domain.Vinculos;

/// <summary>
/// Vínculo de uma conta com uma funerária e o perfil que ela tem lá (AUT-01). A conta é única na plataforma
/// (e-mail único) e a mesma pessoa pode ter vínculo com várias funerárias (DEC-02).
/// </summary>
/// <remarks>
/// Uma conta tem no máximo um vínculo por funerária: o login emite o token com o perfil desse vínculo (AUT-02).
/// Não implementa <c>ITenantEntity</c> de propósito: o login e "minhas funerárias" precisam ler os vínculos
/// do usuário em todas as funerárias.
/// </remarks>
public sealed class UsuarioVinculo : AggregateRoot<Guid>, IAuditableEntity
{
    private UsuarioVinculo(Guid id)
        : base(id)
    {
    }

    private UsuarioVinculo()
    {
    }

    public Guid IdUsuario { get; private set; }

    public Guid IdTenant { get; private set; }

    public PerfilVinculo Perfil { get; private set; }

    public bool Ativo { get; private set; }

    /// <summary>Cadastro do cliente no módulo Clientes. Preenchido quando o perfil é CLIENTE.</summary>
    public Guid? IdCliente { get; private set; }

    /// <summary>Cadastro do terceiro no módulo Terceiros. Preenchido quando o perfil é TERCEIRO.</summary>
    public Guid? IdTerceiro { get; private set; }

    /// <summary>FROTA, CARTORIO_DESPACHANTE ou FORNECEDOR (DEC-06).</summary>
    public string? TipoTerceiro { get; private set; }

    public static UsuarioVinculo Criar(
        Guid idUsuario,
        Guid idTenant,
        PerfilVinculo perfil,
        Guid? idCliente = null,
        Guid? idTerceiro = null,
        string? tipoTerceiro = null)
    {
        if (perfil == PerfilVinculo.Cliente && idCliente is null)
        {
            throw new ArgumentException("Vínculo de cliente precisa do id do cliente.", nameof(idCliente));
        }

        if (perfil == PerfilVinculo.Terceiro && (idTerceiro is null || string.IsNullOrWhiteSpace(tipoTerceiro)))
        {
            throw new ArgumentException("Vínculo de terceiro precisa do id e do tipo do terceiro.", nameof(idTerceiro));
        }

        var vinculo = new UsuarioVinculo(Guid.CreateVersion7())
        {
            IdUsuario = idUsuario,
            IdTenant = idTenant,
            Perfil = perfil,
            Ativo = true,
            IdCliente = perfil == PerfilVinculo.Cliente ? idCliente : null,
            IdTerceiro = perfil == PerfilVinculo.Terceiro ? idTerceiro : null,
            TipoTerceiro = perfil == PerfilVinculo.Terceiro ? tipoTerceiro : null
        };

        vinculo.Raise(new VinculoCriadoDomainEvent(vinculo.Id, idUsuario, idTenant, perfil));
        return vinculo;
    }

    /// <summary>Desativar remove o acesso só a esta funerária: a conta continua valendo para os outros vínculos (AUT-08).</summary>
    public void AlterarStatus(bool ativo) => Ativo = ativo;

    /// <summary>Troca o perfil dentro da equipe (AUT-08). Cliente e terceiro não viram equipe por aqui.</summary>
    public void AlterarPerfil(PerfilVinculo perfil)
    {
        if (!PerfisEquipe.Contem(Perfil) || !PerfisEquipe.Contem(perfil))
        {
            throw new InvalidOperationException("Só é possível trocar o perfil entre os perfis da equipe.");
        }

        Perfil = perfil;
    }
}

/// <summary>Perfis que existem dentro de uma funerária (DEC-06). O ADMIN é global e não tem vínculo.</summary>
public enum PerfilVinculo
{
    Funeraria = 1,
    Funcionario = 2,
    Cliente = 3,
    Terceiro = 4,

    /// <summary>Atendimento e vendas da funerária (DEC-11).</summary>
    Consultor = 5
}

public static class PerfisEquipe
{
    /// <summary>Perfis da equipe da funerária, que o gestor gerencia (AUT-07, AUT-08).</summary>
    public static bool Contem(PerfilVinculo perfil) =>
        perfil is PerfilVinculo.Funeraria or PerfilVinculo.Funcionario or PerfilVinculo.Consultor;
}
