using FluentValidation;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Identidade.Application.Abstractions;
using Funeraria.Modules.Identidade.Domain;
using Funeraria.Modules.Identidade.Domain.Termos;

namespace Funeraria.Modules.Identidade.Application.Termos;

/// <summary>Versões vigentes dos documentos legais (configuração <c>Lgpd</c>).</summary>
public interface ITermosVigentes
{
    string VersaoTermosDeUso { get; }

    string VersaoPoliticaDePrivacidade { get; }
}

/// <summary>
/// Situação do usuário logado com os termos (SEG-01): versões vigentes, quando aceitou cada uma e se falta aceitar.
/// O frontend pede o aceite quando <c>pendente</c> é verdadeiro (versão nova publicada, por exemplo).
/// </summary>
public sealed record SituacaoTermosQuery : IQuery<SituacaoTermosResponse>;

/// <summary>O usuário logado aceita as versões vigentes. Versão diferente da vigente é recusada (o texto mudou).</summary>
public sealed record AceitarTermosCommand(string VersaoTermosDeUso, string VersaoPoliticaDePrivacidade) : ICommand<SituacaoTermosResponse>;

public sealed record SituacaoTermosResponse(DocumentoAceite TermosDeUso, DocumentoAceite PoliticaDePrivacidade, bool Pendente);

/// <param name="VersaoVigente">Versão publicada hoje.</param>
/// <param name="VersaoAceita">Última versão que o usuário aceitou.</param>
/// <param name="AceitoEm">Quando aceitou a última versão.</param>
public sealed record DocumentoAceite(string VersaoVigente, string? VersaoAceita, DateTime? AceitoEm);

internal sealed class AceitarTermosCommandValidator : AbstractValidator<AceitarTermosCommand>
{
    public AceitarTermosCommandValidator()
    {
        RuleFor(c => c.VersaoTermosDeUso).NotEmpty().WithMessage("Informe a versão dos termos de uso aceita.");
        RuleFor(c => c.VersaoPoliticaDePrivacidade).NotEmpty().WithMessage("Informe a versão da política de privacidade aceita.");
    }
}

/// <summary>Registro e consulta do aceite, usados pelos handlers e pelo cadastro de cliente (via IIdentidadeApi).</summary>
public sealed class RegistroDeAceites(
    IAceiteTermosRepository aceites,
    IIdentidadeUnitOfWork unitOfWork,
    ITermosVigentes vigentes,
    IOrigemRequisicao origem,
    TimeProvider timeProvider)
{
    public async Task RegistrarVigentesAsync(Guid idUsuario, CancellationToken cancellationToken)
    {
        DateTime agora = timeProvider.GetUtcNow().UtcDateTime;
        aceites.Adicionar(AceiteTermos.Registrar(idUsuario, DocumentoLegal.TermosDeUso, vigentes.VersaoTermosDeUso, agora, origem.Ip));
        aceites.Adicionar(AceiteTermos.Registrar(idUsuario, DocumentoLegal.PoliticaDePrivacidade, vigentes.VersaoPoliticaDePrivacidade, agora, origem.Ip));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AceiteTermos>> ListarAsync(Guid idUsuario, CancellationToken cancellationToken) =>
        [.. (await aceites.ListarDoUsuarioAsync(idUsuario, cancellationToken)).OrderBy(a => a.AceitoEm)];

    public async Task<SituacaoTermosResponse> SituacaoAsync(Guid idUsuario, CancellationToken cancellationToken)
    {
        IReadOnlyList<AceiteTermos> doUsuario = await aceites.ListarDoUsuarioAsync(idUsuario, cancellationToken);

        DocumentoAceite Situacao(DocumentoLegal documento, string vigente)
        {
            AceiteTermos? ultimo = doUsuario.Where(a => a.Documento == documento).MaxBy(a => a.AceitoEm);
            return new DocumentoAceite(vigente, ultimo?.Versao, ultimo?.AceitoEm);
        }

        DocumentoAceite termos = Situacao(DocumentoLegal.TermosDeUso, vigentes.VersaoTermosDeUso);
        DocumentoAceite politica = Situacao(DocumentoLegal.PoliticaDePrivacidade, vigentes.VersaoPoliticaDePrivacidade);
        bool pendente = termos.VersaoAceita != termos.VersaoVigente || politica.VersaoAceita != politica.VersaoVigente;
        return new SituacaoTermosResponse(termos, politica, pendente);
    }
}

public sealed class SituacaoTermosQueryHandler(RegistroDeAceites registro, ICurrentUser usuario)
    : IQueryHandler<SituacaoTermosQuery, SituacaoTermosResponse>
{
    public async ValueTask<Result<SituacaoTermosResponse>> Handle(SituacaoTermosQuery query, CancellationToken cancellationToken) =>
        usuario.IdUsuario is Guid id
            ? await registro.SituacaoAsync(id, cancellationToken)
            : Result.Failure<SituacaoTermosResponse>(IdentidadeErros.SessaoInvalida);
}

public sealed class AceitarTermosCommandHandler(RegistroDeAceites registro, ITermosVigentes vigentes, ICurrentUser usuario)
    : ICommandHandler<AceitarTermosCommand, SituacaoTermosResponse>
{
    public async ValueTask<Result<SituacaoTermosResponse>> Handle(AceitarTermosCommand command, CancellationToken cancellationToken)
    {
        if (usuario.IdUsuario is not Guid id)
        {
            return IdentidadeErros.SessaoInvalida;
        }

        if (command.VersaoTermosDeUso != vigentes.VersaoTermosDeUso || command.VersaoPoliticaDePrivacidade != vigentes.VersaoPoliticaDePrivacidade)
        {
            return Error.Conflito("Termos.VersaoDesatualizada", "Os termos foram atualizados. Leia a versão nova antes de aceitar.");
        }

        await registro.RegistrarVigentesAsync(id, cancellationToken);
        return await registro.SituacaoAsync(id, cancellationToken);
    }
}
