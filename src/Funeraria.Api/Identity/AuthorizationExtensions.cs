using Funeraria.BuildingBlocks.Application.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Funeraria.Api.Identity;

/// <summary>
/// Policies da matriz do DEC-06 (AUT-04). A validação do JWT é registrada pelo módulo Identidade,
/// que também emite os tokens.
/// </summary>
internal static class AuthorizationExtensions
{
    public static IServiceCollection AddAutorizacao(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            // Toda rota exige login, a menos que esteja marcada com AllowAnonymous (SEG-02).
            // Assim uma rota nova esquecida sem RequireAuthorization não fica pública por engano.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Politicas.Plataforma, p => p.RequireRole(Perfis.Admin))
            .AddPolicy(Politicas.GestaoFuneraria, p => p
                .RequireRole(Perfis.Funeraria)
                .RequireClaim(Claims.IdTenant))
            .AddPolicy(Politicas.EquipeFuneraria, p => p
                .RequireRole(Perfis.Funeraria, Perfis.Funcionario)
                .RequireClaim(Claims.IdTenant))
            .AddPolicy(Politicas.Cliente, p => p
                .RequireRole(Perfis.Cliente)
                .RequireClaim(Claims.IdTenant)
                .RequireClaim(Claims.IdCliente))
            .AddPolicy(Politicas.TerceiroFrota, p => Terceiro(p, TiposTerceiro.Frota))
            .AddPolicy(Politicas.TerceiroDespachante, p => Terceiro(p, TiposTerceiro.CartorioDespachante))
            .AddPolicy(Politicas.TerceiroFornecedor, p => Terceiro(p, TiposTerceiro.Fornecedor));

        return services;
    }

    private static void Terceiro(AuthorizationPolicyBuilder policy, string tipo) =>
        policy
            .RequireRole(Perfis.Terceiro)
            .RequireClaim(Claims.IdTenant)
            .RequireClaim(Claims.IdTerceiro)
            .RequireClaim(Claims.TipoTerceiro, tipo);
}
