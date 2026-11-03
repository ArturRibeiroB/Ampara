using Funeraria.BuildingBlocks.Application.Arquivos;
using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.Modules.Documentos.Application.Documentos;
using Funeraria.Modules.Documentos.Domain.Certidoes;
using Funeraria.Modules.Documentos.Domain.Documentos;
using Funeraria.Modules.Funeral.Contracts.IntegrationEvents;
using Funeraria.Modules.Terceiros.Contracts;

namespace Funeraria.Modules.Documentos.Application.Retencao;

/// <summary>
/// Falecido anonimizado (SEG-01): os arquivos do óbito (documentos e certidão emitida) são excluídos do armazenamento e a
/// solicitação da certidão fica só com status e datas. O arquivo sai antes da linha: se a remoção falhar, a mensagem volta
/// pelo outbox e tenta de novo. Idempotente.
/// </summary>
internal sealed class ExcluirDocumentosDoFalecido(
    IDocumentoRepository documentos,
    ISolicitacaoCertidaoRepository certidoes,
    IArmazenamentoArquivos arquivos,
    IDocumentosUnitOfWork unitOfWork,
    ITerceirosAtribuicoes atribuicoes,
    TimeProvider timeProvider)
    : IIntegrationEventHandler<FalecidoAnonimizadoIntegrationEvent>
{
    public async Task Handle(FalecidoAnonimizadoIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        // A listagem é só leitura: a exclusão carrega cada um de novo, com o token de concorrência.
        foreach (Documento listado in await documentos.ListarDoBeneficiarioAsync(integrationEvent.IdBeneficiario, cancellationToken))
        {
            if (await documentos.ObterAsync(listado.Id, cancellationToken) is Documento documento)
            {
                await arquivos.RemoverAsync(documento.Chave, cancellationToken);
                documentos.Remover(documento);
            }
        }

        SolicitacaoCertidao? certidao = await certidoes.ObterDoBeneficiarioAsync(integrationEvent.IdBeneficiario, cancellationToken);
        certidao?.Anonimizar(timeProvider.GetUtcNow().UtcDateTime);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (certidao is not null)
        {
            // O que o despachante vê no portal também (TER-02).
            await atribuicoes.AnonimizarResumosAsync([certidao.Id], cancellationToken);
        }
    }
}
