using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Funeral.Contracts;
using Funeraria.Modules.Funeral.Domain.Preferencias;

namespace Funeraria.Modules.Funeral.Application;

public sealed class FuneralApi(IPreferenciaRepository preferencias) : IFuneralApi
{
    public async Task<IReadOnlyList<PreferenciaExportadaDto>> ListarPreferenciasDoTitularAsync(Guid idCliente, CancellationToken cancellationToken = default) =>
        (await preferencias.ListarDoTitularAsync(idCliente, cancellationToken))
            .Select(p => new PreferenciaExportadaDto(
                p.IdPessoa,
                NomeApi.De(p.TipoPessoa),
                NomeApi.De(p.Destinacao),
                NomeApi.De(p.TipoCerimonia),
                p.CrencaReligiosa,
                p.Musicas,
                p.Mensagens,
                p.Observacoes,
                [.. p.Itens.Select(i => i.IdItemCatalogo)]))
            .ToList();

    // Beneficiário e funeral entram com os cards Gerar Beneficiário e Registrar Funeral. Até lá não existe nenhum,
    // então as duas travas do Contratos (CON-02 e CON-03) nunca disparam.
    public Task<bool> PessoaEhBeneficiariaAsync(Guid idPessoaCoberta, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> PossuiFuneralEmAndamentoAsync(Guid idContrato, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
