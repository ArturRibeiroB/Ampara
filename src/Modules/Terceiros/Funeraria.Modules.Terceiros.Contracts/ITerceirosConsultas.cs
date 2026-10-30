namespace Funeraria.Modules.Terceiros.Contracts;

/// <summary>Consulta aos terceiros da funerária pelos outros módulos (ex.: fornecedor do item do catálogo, FUN-01). Respeita o tenant atual.</summary>
public interface ITerceirosConsultas
{
    /// <summary>Terceiro da funerária atual, ou nulo se não existir.</summary>
    Task<TerceiroResumoDto?> ObterAsync(Guid idTerceiro, CancellationToken cancellationToken = default);
}

/// <param name="IdTerceiro">Terceiro.</param>
/// <param name="NomeRazaoSocial">Nome mostrado nas telas.</param>
/// <param name="Tipo">FROTA, CARTORIO_DESPACHANTE ou FORNECEDOR.</param>
/// <param name="Ativo">Pode receber serviços: ativo ou ainda convidado (não foi desativado pela funerária).</param>
/// <param name="IdUsuario">Conta do terceiro, para notificá-lo (vazia enquanto o convite não gerou o vínculo).</param>
public sealed record TerceiroResumoDto(Guid IdTerceiro, string NomeRazaoSocial, string Tipo, bool Ativo, Guid? IdUsuario = null);
