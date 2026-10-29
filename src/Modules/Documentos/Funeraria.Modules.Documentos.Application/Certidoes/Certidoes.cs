using FluentValidation;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Arquivos;
using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Clientes.Contracts;
using Funeraria.Modules.Documentos.Application.Documentos;
using Funeraria.Modules.Documentos.Domain.Certidoes;
using Funeraria.Modules.Documentos.Domain.Documentos;
using Funeraria.Modules.Funeral.Contracts;
using Funeraria.Modules.Identidade.Contracts;
using Funeraria.Modules.Notificacoes.Contracts;
using Funeraria.Modules.Terceiros.Contracts;
using Funeraria.Modules.Terceiros.Contracts.IntegrationEvents;

namespace Funeraria.Modules.Documentos.Application.Certidoes;

/// <summary>Pede o registro do óbito e a certidão (CER-03). Exige os documentos obrigatórios anexados.</summary>
public sealed record SolicitarCertidaoCommand(Guid IdBeneficiario) : ICommand<CertidaoResponse>;

/// <summary>Dados e documentos exigidos para o registro, com o prazo legal (CER-03).</summary>
public sealed record ChecklistCertidaoQuery(Guid IdBeneficiario) : IQuery<ChecklistCertidaoResponse>;

public sealed record ObterCertidaoQuery(Guid IdBeneficiario) : IQuery<CertidaoResponse>;

/// <summary>Atribui a solicitação a um funcionário ou a um despachante/cartório parceiro (CER-03). Tipo: FUNCIONARIO ou TERCEIRO.</summary>
public sealed record AtribuirResponsavelCertidaoCommand(Guid IdBeneficiario, string? Tipo, Guid? IdUsuario, Guid? IdTerceiro) : ICommand<CertidaoResponse>;

/// <summary>Etapa registrada pela equipe (CER-04).</summary>
public sealed record AlterarStatusCertidaoCommand(Guid IdBeneficiario, StatusCertidao? Status, string? Observacao, string? Protocolo)
    : ICommand<CertidaoResponse>;

/// <summary>Certidão emitida, registrada pela equipe: PDF e matrícula (CER-04).</summary>
public sealed record RegistrarCertidaoEmitidaCommand(Guid IdBeneficiario, string? NumeroMatricula, ArquivoEnviado? Arquivo) : ICommand<CertidaoResponse>;

/// <summary>Etapa registrada pelo despachante no portal (CER-04). Só a solicitação atribuída a ele.</summary>
public sealed record AlterarStatusCertidaoTerceiroCommand(Guid IdSolicitacao, StatusCertidao? Status, string? Observacao, string? Protocolo)
    : ICommand<CertidaoResponse>;

/// <summary>Certidão emitida, registrada pelo despachante no portal (CER-04).</summary>
public sealed record RegistrarCertidaoEmitidaTerceiroCommand(Guid IdSolicitacao, string? NumeroMatricula, ArquivoEnviado? Arquivo) : ICommand<CertidaoResponse>;

/// <summary>Documento da solicitação para o despachante responsável, por link temporário (CER-03).</summary>
public sealed record BaixarDocumentoCertidaoTerceiroQuery(Guid IdSolicitacao, Guid IdDocumento) : IQuery<LinkDocumentoResponse>;

public sealed record CertidaoResponse(
    Guid IdSolicitacao,
    Guid IdBeneficiario,
    StatusCertidao Status,
    ModoCertidao Modo,
    ResponsavelCertidaoResponse? Responsavel,
    string? Protocolo,
    string? Pendencia,
    string? NumeroMatricula,
    Guid? IdDocumentoCertidao,
    PrazoRegistroResponse Prazo,
    IReadOnlyList<HistoricoCertidaoResponse> Historico)
{
    public static CertidaoResponse De(SolicitacaoCertidao s, DateOnly hoje) => new(
        s.Id,
        s.IdBeneficiario,
        s.Status,
        s.Modo,
        s.TipoDoResponsavel is TipoResponsavel tipo
            ? new ResponsavelCertidaoResponse(tipo, s.IdUsuarioResponsavel, s.IdTerceiroResponsavel, s.NomeResponsavel!)
            : null,
        s.Protocolo,
        s.Pendencia,
        s.NumeroMatricula,
        s.IdDocumentoCertidao,
        PrazoRegistroResponse.De(s.PrazoRegistro, hoje, s.Emitida),
        [.. s.Historico.OrderBy(h => h.Em).Select(h => new HistoricoCertidaoResponse(h.De, h.Para, h.IdUsuario, h.Observacao, h.Em))]);
}

public sealed record ResponsavelCertidaoResponse(TipoResponsavel Tipo, Guid? IdUsuario, Guid? IdTerceiro, string Nome);

/// <param name="Data">Até quando o óbito precisa ser registrado.</param>
/// <param name="DiasRestantes">Negativo quando já passou.</param>
/// <param name="Vencido">Passou do prazo e a certidão não saiu: a equipe precisa agir (alerta do CER-03).</param>
public sealed record PrazoRegistroResponse(DateOnly Data, int DiasRestantes, bool Vencido)
{
    public static PrazoRegistroResponse De(DateOnly prazo, DateOnly hoje, bool emitida) =>
        new(prazo, prazo.DayNumber - hoje.DayNumber, !emitida && hoje > prazo);
}

public sealed record HistoricoCertidaoResponse(StatusCertidao De, StatusCertidao Para, Guid? IdUsuario, string? Observacao, DateTime Em);

public sealed record ChecklistCertidaoResponse(
    DadosRegistroResponse Dados,
    IReadOnlyList<DocumentoExigidoResponse> Documentos,
    PrazoRegistroResponse Prazo,
    bool ProntoParaSolicitar);

public sealed record DadosRegistroResponse(
    string NomeFalecido, DateOnly DataNascimento, DateOnly DataFalecimento, string LocalFalecimento, string NomeDeclarante, string TelefoneDeclarante);

public sealed record DocumentoExigidoResponse(TipoDocumento Tipo, string Descricao, bool Presente);

internal sealed class AtribuirResponsavelCertidaoCommandValidator : AbstractValidator<AtribuirResponsavelCertidaoCommand>
{
    public AtribuirResponsavelCertidaoCommandValidator()
    {
        RuleFor(c => c.Tipo).Must(t => t is "FUNCIONARIO" or "TERCEIRO").WithMessage("Tipo do responsável: FUNCIONARIO ou TERCEIRO.");
        RuleFor(c => c.IdUsuario).NotEmpty().When(c => c.Tipo == "FUNCIONARIO").WithMessage("Informe o funcionário (id_usuario).");
        RuleFor(c => c.IdTerceiro).NotEmpty().When(c => c.Tipo == "TERCEIRO").WithMessage("Informe o despachante (id_terceiro).");
    }
}

internal abstract class StatusCertidaoValidator<T> : AbstractValidator<T>
    where T : class
{
    protected void Regras(Func<T, StatusCertidao?> status, Func<T, string?> observacao)
    {
        RuleFor(c => status(c)).NotNull().WithName("status").WithMessage("Informe o novo status.");
        RuleFor(c => observacao(c)).MaximumLength(1000).WithName("observacao").WithMessage("Observação deve ter no máximo 1000 caracteres.");
    }
}

internal sealed class AlterarStatusCertidaoCommandValidator : StatusCertidaoValidator<AlterarStatusCertidaoCommand>
{
    public AlterarStatusCertidaoCommandValidator() => Regras(c => c.Status, c => c.Observacao);
}

internal sealed class AlterarStatusCertidaoTerceiroCommandValidator : StatusCertidaoValidator<AlterarStatusCertidaoTerceiroCommand>
{
    public AlterarStatusCertidaoTerceiroCommandValidator() => Regras(c => c.Status, c => c.Observacao);
}

internal sealed class RegistrarCertidaoEmitidaCommandValidator : AbstractValidator<RegistrarCertidaoEmitidaCommand>
{
    public RegistrarCertidaoEmitidaCommandValidator()
    {
        RuleFor(c => c.NumeroMatricula).NotEmpty().WithMessage("Número da matrícula é obrigatório.").MaximumLength(60);
        RuleFor(c => c.Arquivo).NotNull().WithName(ValidadorArquivo.Campo).WithMessage("Envie o PDF da certidão.");
    }
}

internal sealed class RegistrarCertidaoEmitidaTerceiroCommandValidator : AbstractValidator<RegistrarCertidaoEmitidaTerceiroCommand>
{
    public RegistrarCertidaoEmitidaTerceiroCommandValidator()
    {
        RuleFor(c => c.NumeroMatricula).NotEmpty().WithMessage("Número da matrícula é obrigatório.").MaximumLength(60);
        RuleFor(c => c.Arquivo).NotNull().WithName(ValidadorArquivo.Campo).WithMessage("Envie o PDF da certidão.");
    }
}

/// <summary>Documentos que o registro do óbito exige (CER-03, DEC-04: declaração de óbito e documentos do falecido e do declarante).</summary>
public static class DocumentosExigidos
{
    public static readonly IReadOnlyList<(TipoDocumento Tipo, string Descricao)> Todos =
    [
        (TipoDocumento.DeclaracaoObito, "Declaração de óbito"),
        (TipoDocumento.DocumentoFalecido, "Documento de identidade do falecido"),
        (TipoDocumento.DocumentoDeclarante, "Documento do declarante")
    ];

    public static string Descricao(TipoDocumento tipo) => tipo switch
    {
        TipoDocumento.DeclaracaoObito => "Declaração de óbito",
        TipoDocumento.DocumentoFalecido => "Documento do falecido",
        TipoDocumento.DocumentoDeclarante => "Documento do declarante",
        TipoDocumento.ComprovanteResidencia => "Comprovante de residência",
        TipoDocumento.CertidaoCivil => "Certidão de nascimento ou casamento",
        TipoDocumento.CertidaoObito => "Certidão de óbito",
        _ => "Outro documento"
    };
}

/// <summary>Regras comuns de etapa e emissão, usadas pela equipe e pelo despachante (CER-04).</summary>
public sealed class RegistroDeCertidao(
    ISolicitacaoCertidaoRepository solicitacoes,
    IDocumentosUnitOfWork unitOfWork,
    GravacaoDeDocumentos gravacao,
    ITerceirosAtribuicoes atribuicoes,
    ICurrentUser usuario,
    TimeProvider timeProvider)
{
    public DateOnly Hoje => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

    public async Task<Result<CertidaoResponse>> MudarStatusAsync(
        SolicitacaoCertidao solicitacao, StatusCertidao destino, string? observacao, string? protocolo, CancellationToken cancellationToken)
    {
        Result mudou = solicitacao.MudarStatus(destino, timeProvider.GetUtcNow().UtcDateTime, usuario.IdUsuario, observacao, protocolo);
        if (mudou.IsFailure)
        {
            return Result.Failure<CertidaoResponse>(mudou.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CertidaoResponse.De(solicitacao, Hoje);
    }

    public async Task<Result<CertidaoResponse>> EmitirAsync(
        SolicitacaoCertidao solicitacao, string numeroMatricula, ArquivoEnviado arquivo, CancellationToken cancellationToken)
    {
        if (solicitacao.Status != StatusCertidao.EmProcessamento)
        {
            return solicitacao.Emitida
                ? CertidaoErros.JaEmitida
                : Result.Failure<CertidaoResponse>(Error.Validacao(
                    "Estado.TransicaoInvalida", "A certidão só é registrada como emitida quando está EM_PROCESSAMENTO."));
        }

        Result<Documento> pdf = await gravacao.GravarAsync(solicitacao.IdBeneficiario, TipoDocumento.CertidaoObito, arquivo, cancellationToken);
        if (pdf.IsFailure)
        {
            return Result.Failure<CertidaoResponse>(pdf.Error);
        }

        solicitacao.RegistrarEmissao(numeroMatricula, pdf.Value.Id, timeProvider.GetUtcNow().UtcDateTime, usuario.IdUsuario);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Saiu da lista de pendências do despachante (TER-02).
        if (solicitacao.IdAtribuicao is Guid idAtribuicao)
        {
            await atribuicoes.EncerrarAsync(idAtribuicao, concluida: true, cancellationToken);
        }

        return CertidaoResponse.De(solicitacao, Hoje);
    }

    /// <summary>Solicitação do beneficiário, com o acesso da equipe interna e do titular (CER-01).</summary>
    public async Task<Result<(BeneficiarioDto Beneficiario, SolicitacaoCertidao? Solicitacao)>> DoBeneficiarioAsync(
        AcessoAosDocumentos acesso, Guid idBeneficiario, CancellationToken cancellationToken)
    {
        Result<BeneficiarioDto> beneficiario = await acesso.BeneficiarioAsync(idBeneficiario, cancellationToken);
        if (beneficiario.IsFailure)
        {
            return Result.Failure<(BeneficiarioDto, SolicitacaoCertidao?)>(beneficiario.Error);
        }

        return (beneficiario.Value, await solicitacoes.ObterDoBeneficiarioAsync(idBeneficiario, cancellationToken));
    }

    /// <summary>Solicitação atribuída ao despachante logado; a de outro responde 404 (TER-02).</summary>
    public async Task<SolicitacaoCertidao?> DoTerceiroAsync(Guid idSolicitacao, CancellationToken cancellationToken)
    {
        SolicitacaoCertidao? solicitacao = await solicitacoes.ObterAsync(idSolicitacao, cancellationToken);
        return solicitacao is not null && solicitacao.IdTerceiroResponsavel is Guid id && id == usuario.IdTerceiro ? solicitacao : null;
    }
}

public sealed class SolicitarCertidaoCommandHandler(
    AcessoAosDocumentos acesso,
    RegistroDeCertidao registro,
    ISolicitacaoCertidaoRepository solicitacoes,
    IDocumentoRepository documentos,
    IDocumentosUnitOfWork unitOfWork,
    SeletorGatewayCertidao seletor,
    IConfiguracaoDocumentos configuracao,
    ITenantContext tenantContext,
    ICurrentUser usuario,
    TimeProvider timeProvider,
    IAuditoria auditoria)
    : ICommandHandler<SolicitarCertidaoCommand, CertidaoResponse>
{
    public async ValueTask<Result<CertidaoResponse>> Handle(SolicitarCertidaoCommand command, CancellationToken cancellationToken)
    {
        var atual = await registro.DoBeneficiarioAsync(acesso, command.IdBeneficiario, cancellationToken);
        if (atual.IsFailure)
        {
            return Result.Failure<CertidaoResponse>(atual.Error);
        }

        (BeneficiarioDto beneficiario, SolicitacaoCertidao? existente) = atual.Value;
        if (existente is not null)
        {
            return existente.Emitida ? CertidaoErros.JaEmitida : CertidaoErros.EmAndamento;
        }

        HashSet<TipoDocumento> anexados = [.. (await documentos.ListarDoBeneficiarioAsync(beneficiario.IdBeneficiario, cancellationToken)).Select(d => d.Tipo)];
        string[] faltando = [.. DocumentosExigidos.Todos.Where(d => !anexados.Contains(d.Tipo)).Select(d => d.Descricao)];
        if (faltando.Length > 0)
        {
            return CertidaoErros.DocumentosFaltando(faltando);
        }

        Guid tenant = tenantContext.RequiredTenantId;
        ICertidaoObitoGateway gateway = await seletor.ObterAsync(tenant, cancellationToken);
        var solicitacao = SolicitacaoCertidao.Solicitar(
            tenant, beneficiario.IdBeneficiario, beneficiario.IdCliente, beneficiario.Nome, gateway.Modo,
            beneficiario.DataFalecimento.AddDays(configuracao.PrazoRegistroObitoDias), timeProvider.GetUtcNow().UtcDateTime, usuario.IdUsuario);
        solicitacoes.Adicionar(solicitacao);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ViolacaoDeUnicidadeException)
        {
            return CertidaoErros.EmAndamento;
        }

        await auditoria.RegistrarAsync(new RegistroAuditoria(
            AcoesAuditadas.CertidaoSolicitada, "SolicitacaoCertidao", solicitacao.Id,
            ValoresNovos: new { id_beneficiario = beneficiario.IdBeneficiario, modo = gateway.Modo.ToString() }), cancellationToken);

        // Modo automático (hoje só o simulado): o gateway já protocola e pode devolver a certidão pronta.
        ResultadoCertidao resultado = await gateway.SolicitarAsync(solicitacao, cancellationToken);
        if (resultado.Protocolo is null)
        {
            return CertidaoResponse.De(solicitacao, registro.Hoje);
        }

        Result<CertidaoResponse> enviada = await registro.MudarStatusAsync(
            solicitacao, StatusCertidao.EmProcessamento, "Enviada automaticamente.", resultado.Protocolo, cancellationToken);
        if (enviada.IsFailure || !resultado.Emitida)
        {
            return enviada;
        }

        await using var pdf = new MemoryStream(resultado.Pdf!);
        return await registro.EmitirAsync(solicitacao, resultado.NumeroMatricula!, new ArquivoEnviado("certidao-obito.pdf", pdf), cancellationToken);
    }
}

public sealed class ChecklistCertidaoQueryHandler(
    AcessoAosDocumentos acesso,
    RegistroDeCertidao registro,
    IDocumentoRepository documentos,
    IConfiguracaoDocumentos configuracao)
    : IQueryHandler<ChecklistCertidaoQuery, ChecklistCertidaoResponse>
{
    public async ValueTask<Result<ChecklistCertidaoResponse>> Handle(ChecklistCertidaoQuery query, CancellationToken cancellationToken)
    {
        var atual = await registro.DoBeneficiarioAsync(acesso, query.IdBeneficiario, cancellationToken);
        if (atual.IsFailure)
        {
            return Result.Failure<ChecklistCertidaoResponse>(atual.Error);
        }

        (BeneficiarioDto b, SolicitacaoCertidao? solicitacao) = atual.Value;
        HashSet<TipoDocumento> anexados = [.. (await documentos.ListarDoBeneficiarioAsync(b.IdBeneficiario, cancellationToken)).Select(d => d.Tipo)];
        DocumentoExigidoResponse[] exigidos = [.. DocumentosExigidos.Todos.Select(d => new DocumentoExigidoResponse(d.Tipo, d.Descricao, anexados.Contains(d.Tipo)))];
        DateOnly prazo = solicitacao?.PrazoRegistro ?? b.DataFalecimento.AddDays(configuracao.PrazoRegistroObitoDias);

        return new ChecklistCertidaoResponse(
            new DadosRegistroResponse(b.Nome, b.DataNascimento, b.DataFalecimento, b.LocalFalecimento, b.NomeResponsavel, b.TelefoneResponsavel),
            exigidos,
            PrazoRegistroResponse.De(prazo, registro.Hoje, solicitacao?.Emitida == true),
            solicitacao is null && exigidos.All(d => d.Presente));
    }
}

public sealed class ObterCertidaoQueryHandler(AcessoAosDocumentos acesso, RegistroDeCertidao registro) : IQueryHandler<ObterCertidaoQuery, CertidaoResponse>
{
    public async ValueTask<Result<CertidaoResponse>> Handle(ObterCertidaoQuery query, CancellationToken cancellationToken)
    {
        var atual = await registro.DoBeneficiarioAsync(acesso, query.IdBeneficiario, cancellationToken);
        if (atual.IsFailure)
        {
            return Result.Failure<CertidaoResponse>(atual.Error);
        }

        return atual.Value.Solicitacao is SolicitacaoCertidao s ? CertidaoResponse.De(s, registro.Hoje) : CertidaoErros.NaoEncontrada;
    }
}

public sealed class AtribuirResponsavelCertidaoCommandHandler(
    AcessoAosDocumentos acesso,
    RegistroDeCertidao registro,
    IDocumentoRepository documentos,
    IDocumentosUnitOfWork unitOfWork,
    IIdentidadeApi identidade,
    ITerceirosConsultas terceiros,
    ITerceirosAtribuicoes atribuicoes,
    ITenantContext tenantContext)
    : ICommandHandler<AtribuirResponsavelCertidaoCommand, CertidaoResponse>
{
    public async ValueTask<Result<CertidaoResponse>> Handle(AtribuirResponsavelCertidaoCommand command, CancellationToken cancellationToken)
    {
        var atual = await registro.DoBeneficiarioAsync(acesso, command.IdBeneficiario, cancellationToken);
        if (atual.IsFailure)
        {
            return Result.Failure<CertidaoResponse>(atual.Error);
        }

        (BeneficiarioDto b, SolicitacaoCertidao? solicitacao) = atual.Value;
        if (solicitacao is null)
        {
            return CertidaoErros.NaoEncontrada;
        }

        Result<Guid?> anterior;
        if (command.Tipo == "FUNCIONARIO")
        {
            MembroEquipeDto? membro = await identidade.ObterMembroEquipeAsync(command.IdUsuario!.Value, tenantContext.RequiredTenantId, cancellationToken);
            if (membro is null)
            {
                return CertidaoErros.ResponsavelInvalido;
            }

            anterior = solicitacao.AtribuirAFuncionario(membro.IdUsuario, membro.Nome);
        }
        else
        {
            TerceiroResumoDto? despachante = await terceiros.ObterAsync(command.IdTerceiro!.Value, cancellationToken);
            if (despachante is not { Tipo: "CARTORIO_DESPACHANTE", Ativo: true })
            {
                return CertidaoErros.ResponsavelInvalido;
            }

            anterior = solicitacao.AtribuirATerceiro(despachante.IdTerceiro, despachante.NomeRazaoSocial);
        }

        if (anterior.IsFailure)
        {
            return Result.Failure<CertidaoResponse>(anterior.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // O despachante anterior perde a atribuição; o novo recebe só os dados e documentos desta solicitação (TER-02).
        if (anterior.Value is Guid idAnterior)
        {
            await atribuicoes.EncerrarAsync(idAnterior, concluida: false, cancellationToken);
        }

        if (solicitacao.IdTerceiroResponsavel is Guid idTerceiro)
        {
            DocumentoAtribuicao[] docs = [.. (await documentos.ListarDoBeneficiarioAsync(b.IdBeneficiario, cancellationToken))
                .Where(d => d.Tipo != TipoDocumento.CertidaoObito)
                .Select(d => new DocumentoAtribuicao(DocumentosExigidos.Descricao(d.Tipo), d.Id))];
            Result<Guid> atribuicao = await atribuicoes.AtribuirAsync(
                new NovaAtribuicao(idTerceiro, solicitacao.Id, new ResumoCertidao(b.Nome, b.DataFalecimento, b.LocalFalecimento, docs)), cancellationToken);
            if (atribuicao.IsFailure)
            {
                return Result.Failure<CertidaoResponse>(atribuicao.Error);
            }

            solicitacao.VincularAtribuicao(atribuicao.Value);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return CertidaoResponse.De(solicitacao, registro.Hoje);
    }
}

public sealed class AlterarStatusCertidaoCommandHandler(AcessoAosDocumentos acesso, RegistroDeCertidao registro)
    : ICommandHandler<AlterarStatusCertidaoCommand, CertidaoResponse>
{
    public async ValueTask<Result<CertidaoResponse>> Handle(AlterarStatusCertidaoCommand command, CancellationToken cancellationToken)
    {
        var atual = await registro.DoBeneficiarioAsync(acesso, command.IdBeneficiario, cancellationToken);
        if (atual.IsFailure)
        {
            return Result.Failure<CertidaoResponse>(atual.Error);
        }

        return atual.Value.Solicitacao is SolicitacaoCertidao s
            ? await registro.MudarStatusAsync(s, command.Status!.Value, command.Observacao, command.Protocolo, cancellationToken)
            : CertidaoErros.NaoEncontrada;
    }
}

public sealed class RegistrarCertidaoEmitidaCommandHandler(AcessoAosDocumentos acesso, RegistroDeCertidao registro)
    : ICommandHandler<RegistrarCertidaoEmitidaCommand, CertidaoResponse>
{
    public async ValueTask<Result<CertidaoResponse>> Handle(RegistrarCertidaoEmitidaCommand command, CancellationToken cancellationToken)
    {
        var atual = await registro.DoBeneficiarioAsync(acesso, command.IdBeneficiario, cancellationToken);
        if (atual.IsFailure)
        {
            return Result.Failure<CertidaoResponse>(atual.Error);
        }

        return atual.Value.Solicitacao is SolicitacaoCertidao s
            ? await registro.EmitirAsync(s, command.NumeroMatricula!, command.Arquivo!, cancellationToken)
            : CertidaoErros.NaoEncontrada;
    }
}

public sealed class AlterarStatusCertidaoTerceiroCommandHandler(RegistroDeCertidao registro)
    : ICommandHandler<AlterarStatusCertidaoTerceiroCommand, CertidaoResponse>
{
    public async ValueTask<Result<CertidaoResponse>> Handle(AlterarStatusCertidaoTerceiroCommand command, CancellationToken cancellationToken) =>
        await registro.DoTerceiroAsync(command.IdSolicitacao, cancellationToken) is SolicitacaoCertidao s
            ? await registro.MudarStatusAsync(s, command.Status!.Value, command.Observacao, command.Protocolo, cancellationToken)
            : CertidaoErros.NaoEncontrada;
}

public sealed class RegistrarCertidaoEmitidaTerceiroCommandHandler(RegistroDeCertidao registro)
    : ICommandHandler<RegistrarCertidaoEmitidaTerceiroCommand, CertidaoResponse>
{
    public async ValueTask<Result<CertidaoResponse>> Handle(RegistrarCertidaoEmitidaTerceiroCommand command, CancellationToken cancellationToken) =>
        await registro.DoTerceiroAsync(command.IdSolicitacao, cancellationToken) is SolicitacaoCertidao s
            ? await registro.EmitirAsync(s, command.NumeroMatricula!, command.Arquivo!, cancellationToken)
            : CertidaoErros.NaoEncontrada;
}

public sealed class BaixarDocumentoCertidaoTerceiroQueryHandler(RegistroDeCertidao registro, IDocumentoRepository documentos, LinksDeDocumentos links)
    : IQueryHandler<BaixarDocumentoCertidaoTerceiroQuery, LinkDocumentoResponse>
{
    public async ValueTask<Result<LinkDocumentoResponse>> Handle(BaixarDocumentoCertidaoTerceiroQuery query, CancellationToken cancellationToken)
    {
        SolicitacaoCertidao? solicitacao = await registro.DoTerceiroAsync(query.IdSolicitacao, cancellationToken);
        Documento? documento = solicitacao is null ? null : await documentos.ObterAsync(query.IdDocumento, cancellationToken);
        return documento is null || documento.IdBeneficiario != solicitacao!.IdBeneficiario
            ? DocumentoErros.NaoEncontrado
            : await links.GerarAsync(documento, cancellationToken);
    }
}

/// <summary>Certidão emitida: avisa o titular do contrato (CER-04).</summary>
internal sealed class AvisarTitularDaCertidao(ISolicitacaoCertidaoRepository solicitacoes, IClientesApi clientes, INotificacoes notificacoes)
    : IDomainEventHandler<CertidaoEmitidaDomainEvent>
{
    public async Task Handle(CertidaoEmitidaDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await solicitacoes.ObterAsync(domainEvent.IdSolicitacao, cancellationToken) is not SolicitacaoCertidao s
            || await clientes.ObterTitularAsync(s.IdCliente, cancellationToken) is not TitularDto titular)
        {
            return;
        }

        await notificacoes.NotificarAsync(new NovaNotificacao(
            [titular.IdUsuario], s.TenantId, "CERTIDAO_EMITIDA", $"Certidão de óbito de {s.NomeFalecido}",
            "A certidão foi emitida e está disponível nos documentos.", $"/beneficiarios/{s.IdBeneficiario}/certidao"), cancellationToken);
    }
}

/// <summary>O despachante recusou a certidão no portal (TER-02): a equipe escolhe outro responsável.</summary>
internal sealed class CertidaoRecusadaPeloDespachante(ISolicitacaoCertidaoRepository solicitacoes, IDocumentosUnitOfWork unitOfWork)
    : IIntegrationEventHandler<AtribuicaoRecusadaIntegrationEvent>
{
    public async Task Handle(AtribuicaoRecusadaIntegrationEvent evento, CancellationToken cancellationToken)
    {
        if (evento.Tipo == "CERTIDAO" && await solicitacoes.ObterAsync(evento.IdOrigem, cancellationToken) is SolicitacaoCertidao s
            && s.IdAtribuicao == evento.IdAtribuicao)
        {
            s.LiberarResponsavel();
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}

/// <summary>
/// Alerta do prazo legal do registro do óbito (CER-03): avisa os gestores das solicitações ainda não emitidas que vencem
/// até amanhã. Roda uma vez por dia, para todas as funerárias.
/// </summary>
public sealed class AlertaPrazoCertidoes(
    ISolicitacaoCertidaoRepository solicitacoes,
    IIdentidadeApi identidade,
    INotificacoes notificacoes,
    TimeProvider timeProvider)
{
    public async Task<int> ExecutarAsync(CancellationToken cancellationToken)
    {
        DateOnly hoje = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        IReadOnlyList<SolicitacaoCertidao> pendentes = await solicitacoes.ListarComPrazoAteAsync(hoje.AddDays(1), cancellationToken);
        foreach (SolicitacaoCertidao s in pendentes)
        {
            string quando = s.PrazoRegistro < hoje ? $"venceu em {s.PrazoRegistro:dd/MM/yyyy}" : $"vence em {s.PrazoRegistro:dd/MM/yyyy}";
            await notificacoes.NotificarAsync(new NovaNotificacao(
                await identidade.ListarGestoresAtivosAsync(s.TenantId, cancellationToken), s.TenantId, "CERTIDAO_PRAZO",
                $"Prazo do registro do óbito: {s.NomeFalecido}", $"O prazo legal {quando}. Status: {NomeApi.De(s.Status)}.",
                $"/beneficiarios/{s.IdBeneficiario}/certidao"), cancellationToken);
        }

        return pendentes.Count;
    }
}
