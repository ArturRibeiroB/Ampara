namespace Funeraria.BuildingBlocks.Application.Authorization;

/// <summary>Perfis da plataforma (DEC-06). Valor da claim <c>role</c> do JWT.</summary>
public static class Perfis
{
    public const string Admin = "ADMIN";
    public const string Funeraria = "FUNERARIA";
    public const string Funcionario = "FUNCIONARIO";
    public const string Cliente = "CLIENTE";
    public const string Terceiro = "TERCEIRO";
}

/// <summary>Tipos de terceiro (DEC-06). Valor da claim <c>tipo_terceiro</c>.</summary>
public static class TiposTerceiro
{
    public const string Frota = "FROTA";
    public const string CartorioDespachante = "CARTORIO_DESPACHANTE";
    public const string Fornecedor = "FORNECEDOR";
}

/// <summary>Policies nomeadas da matriz do DEC-06 (AUT-04). Use estas nos endpoints, não os perfis.</summary>
public static class Politicas
{
    public const string Plataforma = nameof(Plataforma);
    public const string GestaoFuneraria = nameof(GestaoFuneraria);
    public const string EquipeFuneraria = nameof(EquipeFuneraria);
    public const string Cliente = nameof(Cliente);

    /// <summary>Cliente ou equipe da funerária. O caso de uso limita o cliente aos próprios registros (DEC-06).</summary>
    public const string ClienteOuEquipe = nameof(ClienteOuEquipe);
    public const string TerceiroFrota = nameof(TerceiroFrota);
    public const string TerceiroDespachante = nameof(TerceiroDespachante);
    public const string TerceiroFornecedor = nameof(TerceiroFornecedor);
}

/// <summary>Nomes das claims do JWT (AUT-02).</summary>
public static class Claims
{
    public const string IdUsuario = "sub";
    public const string Email = "email";
    public const string Perfil = "role";
    public const string IdTenant = "id_tenant";
    public const string IdCliente = "id_cliente";
    public const string IdTerceiro = "id_terceiro";
    public const string TipoTerceiro = "tipo_terceiro";
}
