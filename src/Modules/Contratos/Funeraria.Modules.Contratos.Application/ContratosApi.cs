using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Contratos.Contracts;
using Funeraria.Modules.Contratos.Application.Queries;
using Funeraria.Modules.Contratos.Domain.Contratos;

namespace Funeraria.Modules.Contratos.Application;

public sealed class ContratosApi(IContratoRepository contratos, TimeProvider timeProvider) : IContratosApi
{
    public async Task<Guid?> PlanoQueCobreAsync(Guid idCliente, Guid? idDependente, CancellationToken cancellationToken = default) =>
        (await contratos.ListarDoClienteAsync(idCliente, cancellationToken))
            .Where(c => c.Status != StatusContrato.Cancelado)
            .Where(c => c.PessoasCobertas.Any(p => p.DataRemocao is null && (idDependente is null
                ? p.Tipo == TipoPessoaCoberta.Titular
                : p.IdDependente == idDependente)))
            .OrderByDescending(c => c.Status == StatusContrato.Ativo)
            .ThenByDescending(c => c.DataContratacao)
            .Select(c => (Guid?)c.IdPlano)
            .FirstOrDefault();

    public async Task<Guid?> PlanoDoContratoAsync(Guid idContrato, CancellationToken cancellationToken = default) =>
        (await contratos.ObterAsync(idContrato, cancellationToken))?.IdPlano;

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

    public async Task<IReadOnlyList<Guid>> ClientesComContratoAsync(string? status, CancellationToken cancellationToken = default)
    {
        StatusContrato? lido = null;
        if (status is not null)
        {
            lido = StatusFiltro.Ler(status) ?? throw new ArgumentException($"Status de contrato inválido: {status}.", nameof(status));
        }

        return await contratos.ClientesComContratoAsync(lido, cancellationToken);
    }

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
