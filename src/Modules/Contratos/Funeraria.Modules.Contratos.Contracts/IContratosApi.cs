namespace Funeraria.Modules.Contratos.Contracts;

/// <summary>API pública do módulo Contratos para os outros módulos.</summary>
public interface IContratosApi
{
    /// <summary>Se o dependente está coberto por algum contrato não cancelado (CLI-02: impede a remoção).</summary>
    Task<bool> DependentePossuiContratoAtivoAsync(Guid idDependente, CancellationToken cancellationToken = default);
}
