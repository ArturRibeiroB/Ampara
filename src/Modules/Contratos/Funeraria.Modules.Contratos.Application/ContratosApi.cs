using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Contratos.Contracts;
using Funeraria.Modules.Contratos.Domain.Contratos;

namespace Funeraria.Modules.Contratos.Application;

public sealed class ContratosApi(IContratoRepository contratos, TimeProvider timeProvider) : IContratosApi
{
    public async Task<IReadOnlyList<ContratoDoClienteDto>> ListarDoClienteAsync(Guid idCliente, CancellationToken cancellationToken = default) =>
        (await contratos.ListarDoClienteAsync(idCliente, cancellationToken))
            .Select(c => new ContratoDoClienteDto(
                c.Id,
                c.NomePlano,
                NomeApi.De(c.Status),
                NomeApi.De(c.Periodicidade),
                c.ValorTotal,
                c.DataContratacao,
                c.DataFim,
                c.PessoasCobertas.Count(p => p.DataRemocao is null)))
            .ToList();

    public async Task<ElegibilidadeDto?> VerificarElegibilidadeAsync(Guid idPessoaCoberta, CancellationToken cancellationToken = default)
    {
        Contrato? contrato = await contratos.ObterPorPessoaCobertaAsync(idPessoaCoberta, cancellationToken);
        PessoaCoberta? pessoa = contrato?.PessoasCobertas.FirstOrDefault(p => p.Id == idPessoaCoberta);
        if (contrato is null || pessoa is null)
        {
            return null;
        }

        Elegibilidade e = contrato.AvaliarElegibilidade(pessoa, DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime));
        return new ElegibilidadeDto(pessoa.Id, contrato.Id, contrato.IdCliente, pessoa.IdDependente, e.Elegivel, e.Motivo, e.CarenciaCumpridaEm);
    }

    public async Task<bool> DependentePossuiContratoAtivoAsync(Guid idDependente, CancellationToken cancellationToken = default) =>
        (await contratos.DependentesCobertosAsync([idDependente], cancellationToken)).Count > 0;
}
