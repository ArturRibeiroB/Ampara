using FluentValidation;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Arquivos;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Documentos.Domain.Documentos;
using Funeraria.Modules.Funeral.Contracts;
using Microsoft.Extensions.Logging;

namespace Funeraria.Modules.Documentos.Application.Documentos;

public interface IDocumentosUnitOfWork : IUnitOfWork;

/// <summary>Configuração do módulo (seção "Documentos"), lida na Infrastructure.</summary>
public interface IConfiguracaoDocumentos
{
    /// <summary>PDF, JPEG ou PNG até o tamanho máximo configurado (CER-01).</summary>
    PoliticaArquivo Politica { get; }

    /// <summary>Validade do link de download (CER-01).</summary>
    TimeSpan ValidadeLink { get; }

    /// <summary>
    /// Prazo para registrar o óbito, contado do falecimento (CER-03). A Lei 6.015/1973 (art. 78) prevê 24 horas, com extensão
    /// para até 15 dias em alguns casos: a funerária confirma o prazo aplicável e o valor fica em configuração.
    /// </summary>
    int PrazoRegistroObitoDias { get; }

    /// <summary>Permite o modo SIMULADO da certidão (CER-02). Só em desenvolvimento e testes.</summary>
    bool PermitirModoSimulado { get; }
}

/// <summary>Anexa um documento ao beneficiário (CER-01): PDF, JPEG ou PNG até o tamanho da configuração.</summary>
/// <remarks>O tipo vem como texto do formulário (DECLARACAO_OBITO): o bind de enum do form não aceita o formato da API.</remarks>
public sealed record AnexarDocumentoCommand(Guid IdBeneficiario, string? Tipo, ArquivoEnviado? Arquivo) : ICommand<DocumentoResponse>
{
    public TipoDocumento? TipoLido =>
        Tipo is not null
        && Enum.TryParse(Tipo.Replace("_", string.Empty, StringComparison.Ordinal), ignoreCase: true, out TipoDocumento lido)
        && Enum.IsDefined(lido)
            ? lido
            : null;
}

public sealed record ListarDocumentosQuery(Guid IdBeneficiario) : IQuery<IReadOnlyList<DocumentoResponse>>;

/// <summary>Link temporário para baixar o documento (CER-01). O armazenamento continua privado.</summary>
public sealed record BaixarDocumentoQuery(Guid IdBeneficiario, Guid IdDocumento) : IQuery<LinkDocumentoResponse>;

public sealed record RemoverDocumentoCommand(Guid IdBeneficiario, Guid IdDocumento) : ICommand;

public sealed record DocumentoResponse(Guid IdDocumento, TipoDocumento Tipo, string NomeOriginal, string ContentType, long Tamanho, DateTime EnviadoEm)
{
    public static DocumentoResponse De(Documento d) => new(d.Id, d.Tipo, d.NomeOriginal, d.ContentType, d.Tamanho, d.EnviadoEm);
}

public sealed record LinkDocumentoResponse(Uri Url, DateTime ExpiraEm);

internal sealed class AnexarDocumentoCommandValidator : AbstractValidator<AnexarDocumentoCommand>
{
    public AnexarDocumentoCommandValidator()
    {
        RuleFor(c => c.TipoLido).NotNull().WithName("tipo").WithMessage("Tipo inválido ou ausente. Use DECLARACAO_OBITO, DOCUMENTO_FALECIDO, "
            + "DOCUMENTO_DECLARANTE, COMPROVANTE_RESIDENCIA, CERTIDAO_CIVIL ou OUTRO.");
        RuleFor(c => c.TipoLido).NotEqual(TipoDocumento.CertidaoObito).WithName("tipo")
            .WithMessage("A certidão de óbito emitida é registrada em POST /beneficiarios/{id}/certidao/emitida.");
        RuleFor(c => c.Arquivo).NotNull().WithName(ValidadorArquivo.Campo).WithMessage("Envie o arquivo.");
    }
}

/// <summary>Quem vê os documentos do falecido: a equipe interna da funerária e o titular do contrato (CER-01, DEC-06).</summary>
public sealed class AcessoAosDocumentos(IFuneralApi funeral, ICurrentUser usuario)
{
    public async Task<Result<BeneficiarioDto>> BeneficiarioAsync(Guid idBeneficiario, CancellationToken cancellationToken)
    {
        BeneficiarioDto? beneficiario = await funeral.ObterBeneficiarioAsync(idBeneficiario, cancellationToken);
        if (beneficiario is null || (usuario.Perfil == Perfis.Cliente && beneficiario.IdCliente != usuario.IdCliente))
        {
            return DocumentoErros.BeneficiarioNaoEncontrado;
        }

        return usuario.Perfil == Perfis.Consultor ? DocumentoErros.AcessoNegado : beneficiario;
    }
}

/// <summary>Grava o arquivo no armazenamento da funerária e registra o documento (CER-01, CER-04).</summary>
public sealed class GravacaoDeDocumentos(
    IArmazenamentoArquivos armazenamento,
    IDocumentoRepository documentos,
    IDocumentosUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    IConfiguracaoDocumentos configuracao,
    TimeProvider timeProvider)
{
    public async Task<Result<Documento>> GravarAsync(Guid idBeneficiario, TipoDocumento tipo, ArquivoEnviado arquivo, CancellationToken cancellationToken)
    {
        Guid tenant = tenantContext.RequiredTenantId;
        Result<ArquivoSalvo> salvo = await armazenamento.SalvarAsync(tenant, "documentos", arquivo, configuracao.Politica, cancellationToken);
        if (salvo.IsFailure)
        {
            return salvo.Error;
        }

        var documento = Documento.Anexar(tenant, idBeneficiario, tipo, arquivo.NomeOriginal, salvo.Value.Chave, salvo.Value.ContentType, salvo.Value.Tamanho, timeProvider.GetUtcNow().UtcDateTime);
        documentos.Adicionar(documento);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return documento;
    }
}

public sealed class AnexarDocumentoCommandHandler(AcessoAosDocumentos acesso, GravacaoDeDocumentos gravacao)
    : ICommandHandler<AnexarDocumentoCommand, DocumentoResponse>
{
    public async ValueTask<Result<DocumentoResponse>> Handle(AnexarDocumentoCommand command, CancellationToken cancellationToken)
    {
        Result<BeneficiarioDto> beneficiario = await acesso.BeneficiarioAsync(command.IdBeneficiario, cancellationToken);
        if (beneficiario.IsFailure)
        {
            return Result.Failure<DocumentoResponse>(beneficiario.Error);
        }

        Result<Documento> gravado = await gravacao.GravarAsync(command.IdBeneficiario, command.TipoLido!.Value, command.Arquivo!, cancellationToken);
        return gravado.IsSuccess ? DocumentoResponse.De(gravado.Value) : Result.Failure<DocumentoResponse>(gravado.Error);
    }
}

public sealed class ListarDocumentosQueryHandler(AcessoAosDocumentos acesso, IDocumentoRepository documentos)
    : IQueryHandler<ListarDocumentosQuery, IReadOnlyList<DocumentoResponse>>
{
    public async ValueTask<Result<IReadOnlyList<DocumentoResponse>>> Handle(ListarDocumentosQuery query, CancellationToken cancellationToken)
    {
        Result<BeneficiarioDto> beneficiario = await acesso.BeneficiarioAsync(query.IdBeneficiario, cancellationToken);
        return beneficiario.IsFailure
            ? Result.Failure<IReadOnlyList<DocumentoResponse>>(beneficiario.Error)
            : (await documentos.ListarDoBeneficiarioAsync(query.IdBeneficiario, cancellationToken)).Select(DocumentoResponse.De).ToList();
    }
}

public sealed class BaixarDocumentoQueryHandler(
    AcessoAosDocumentos acesso,
    IDocumentoRepository documentos,
    LinksDeDocumentos links)
    : IQueryHandler<BaixarDocumentoQuery, LinkDocumentoResponse>
{
    public async ValueTask<Result<LinkDocumentoResponse>> Handle(BaixarDocumentoQuery query, CancellationToken cancellationToken)
    {
        Result<BeneficiarioDto> beneficiario = await acesso.BeneficiarioAsync(query.IdBeneficiario, cancellationToken);
        if (beneficiario.IsFailure)
        {
            return Result.Failure<LinkDocumentoResponse>(beneficiario.Error);
        }

        Documento? documento = await documentos.ObterAsync(query.IdDocumento, cancellationToken);
        return documento is null || documento.IdBeneficiario != query.IdBeneficiario
            ? DocumentoErros.NaoEncontrado
            : await links.GerarAsync(documento, cancellationToken);
    }
}

/// <summary>Link temporário de download, com a validade da configuração (CER-01).</summary>
public sealed class LinksDeDocumentos(IArmazenamentoArquivos armazenamento, IConfiguracaoDocumentos configuracao, TimeProvider timeProvider)
{
    public async Task<LinkDocumentoResponse> GerarAsync(Documento documento, CancellationToken cancellationToken)
    {
        TimeSpan validade = configuracao.ValidadeLink;
        Uri url = await armazenamento.GerarLinkTemporarioAsync(documento.Chave, validade, cancellationToken);
        return new LinkDocumentoResponse(url, timeProvider.GetUtcNow().UtcDateTime + validade);
    }
}

public sealed class RemoverDocumentoCommandHandler(
    AcessoAosDocumentos acesso,
    IDocumentoRepository documentos,
    IDocumentosUnitOfWork unitOfWork,
    IArmazenamentoArquivos armazenamento,
    ILogger<RemoverDocumentoCommandHandler> logger)
    : ICommandHandler<RemoverDocumentoCommand>
{
    public async ValueTask<Result> Handle(RemoverDocumentoCommand command, CancellationToken cancellationToken)
    {
        Result<BeneficiarioDto> beneficiario = await acesso.BeneficiarioAsync(command.IdBeneficiario, cancellationToken);
        if (beneficiario.IsFailure)
        {
            return beneficiario.Error;
        }

        Documento? documento = await documentos.ObterAsync(command.IdDocumento, cancellationToken);
        if (documento is null || documento.IdBeneficiario != command.IdBeneficiario)
        {
            return DocumentoErros.NaoEncontrado;
        }

        if (documento.Tipo == TipoDocumento.CertidaoObito)
        {
            return DocumentoErros.CertidaoEmitidaNaoSaiPorAqui;
        }

        documentos.Remover(documento);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        try
        {
            await armazenamento.RemoverAsync(documento.Chave, cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // Sem dados pessoais no log: só o id (CER-02).
            logger.LogWarning(e, "Não foi possível remover o arquivo do documento {IdDocumento}.", documento.Id);
        }

        return Result.Success();
    }
}
