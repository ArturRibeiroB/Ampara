using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Funeral.Contracts;
using Funeraria.Modules.Funeral.Domain.Beneficiarios;
using Funeraria.Modules.Funeral.Domain.Funerais;
using Funeraria.Modules.Funeral.Domain.Preferencias;

namespace Funeraria.Modules.Funeral.Application;

public sealed class FuneralApi(IPreferenciaRepository preferencias, IBeneficiarioRepository beneficiarios, IFuneralRepository funerais)
    : IFuneralApi
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

    public async Task<BeneficiarioDto?> ObterBeneficiarioAsync(Guid idBeneficiario, CancellationToken cancellationToken = default) =>
        await beneficiarios.ObterAsync(idBeneficiario, cancellationToken) is { } b
            ? new BeneficiarioDto(b.Id, b.IdCliente, b.Nome, b.DataNascimento, b.DataFalecimento, b.LocalFalecimento, b.Responsavel.Nome, b.Responsavel.Telefone)
            : null;

    public Task<bool> PessoaEhBeneficiariaAsync(Guid idPessoaCoberta, CancellationToken cancellationToken = default) =>
        beneficiarios.ExisteDaPessoaAsync(idPessoaCoberta, cancellationToken);

    public Task<bool> PossuiFuneralEmAndamentoAsync(Guid idContrato, CancellationToken cancellationToken = default) =>
        funerais.ExisteEmAndamentoNoContratoAsync(idContrato, cancellationToken);
}
