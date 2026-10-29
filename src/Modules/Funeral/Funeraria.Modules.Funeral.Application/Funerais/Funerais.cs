using FluentValidation;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Application.Paginacao;
using Funeraria.BuildingBlocks.Domain.Estados;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Clientes.Contracts;
using Funeraria.Modules.Contratos.Contracts;
using Funeraria.Modules.Funeral.Application.Abstractions;
using Funeraria.Modules.Funeral.Contracts.IntegrationEvents;
using Funeraria.Modules.Funeral.Domain.Beneficiarios;
using Funeraria.Modules.Funeral.Domain.Funerais;
using Funeraria.Modules.Funeral.Domain.Preferencias;
using Funeraria.Modules.Identidade.Contracts;
using FuneralEntity = Funeraria.Modules.Funeral.Domain.Funerais.Funeral;

namespace Funeraria.Modules.Funeral.Application.Funerais;

/// <summary>Registra o falecimento de uma pessoa coberta (card Gerar Beneficiário). Exige elegibilidade (CON-04).</summary>
public sealed record RegistrarBeneficiarioCommand(
    Guid? IdPessoaCoberta,
    DateOnly? DataFalecimento,
    string? LocalFalecimento,
    string? NomeResponsavel,
    string? TelefoneResponsavel,
    string? ParentescoResponsavel)
    : ICommand<BeneficiarioResponse>;

public sealed record ObterBeneficiarioQuery(Guid IdBeneficiario) : IQuery<BeneficiarioResponse>;

/// <summary>Abre o atendimento funeral do beneficiário, em REGISTRADO (card Registrar Funeral).</summary>
public sealed record RegistrarFuneralCommand(Guid? IdBeneficiario, DateTime? DataHoraPrevista, Destinacao? Destinacao, string? Observacoes)
    : ICommand<FuneralResponse>;

/// <summary>Muda a etapa do funeral (FUN-06). Só transições permitidas; cada mudança vai para o histórico e avisa a família.</summary>
public sealed record AlterarStatusFuneralCommand(Guid IdFuneral, StatusFuneral? Status, string? Observacao) : ICommand<FuneralResponse>;

/// <summary>Coloca um gestor ou funcionário na equipe do funeral, ou troca a função dele (FUN-08).</summary>
public sealed record AtribuirMembroFuneralCommand(Guid IdFuneral, Guid IdUsuario, FuncaoEquipe? Funcao) : ICommand<FuneralResponse>;

public sealed record RemoverMembroFuneralCommand(Guid IdFuneral, Guid IdUsuario) : ICommand<FuneralResponse>;

/// <summary>Funerais em andamento em que o usuário logado está na equipe (FUN-08).</summary>
public sealed record MeusFuneraisQuery : IQuery<IReadOnlyList<FuneralResumoResponse>>;

/// <summary>Detalhe do funeral (FUN-07): a equipe vê qualquer um; o titular, os dos próprios contratos.</summary>
public sealed record ObterFuneralQuery(Guid IdFuneral) : IQuery<FuneralResponse>;

/// <summary>Listagem da equipe (FUN-07), por status e período da cerimônia.</summary>
public sealed record ListarFuneraisQuery(PaginaRequest Pagina, string? Status, DateOnly? DataInicio, DateOnly? DataFim)
    : IQuery<PaginaResult<FuneralResumoResponse>>;

public sealed record BeneficiarioResponse(
    Guid IdBeneficiario,
    Guid IdPessoaCoberta,
    Guid IdContrato,
    Guid IdCliente,
    Guid? IdDependente,
    string Nome,
    DateOnly DataNascimento,
    DateOnly DataFalecimento,
    string LocalFalecimento,
    ResponsavelFamilia Responsavel)
{
    public static BeneficiarioResponse De(Beneficiario b) => new(
        b.Id, b.IdPessoaCoberta, b.IdContrato, b.IdCliente, b.IdDependente, b.Nome, b.DataNascimento, b.DataFalecimento,
        b.LocalFalecimento, b.Responsavel);
}

public sealed record FuneralResponse(
    Guid IdFuneral,
    Guid IdBeneficiario,
    Guid IdContrato,
    string NomeFalecido,
    StatusFuneral Status,
    DateTime DataHoraPrevista,
    Destinacao Destinacao,
    string? Observacoes,
    IReadOnlyList<StatusFuneral> ProximosStatus,
    IReadOnlyList<MembroEquipeResponse> Equipe,
    IReadOnlyList<HistoricoStatusResponse> Historico)
{
    public static FuneralResponse De(FuneralEntity f) => new(
        f.Id,
        f.IdBeneficiario,
        f.IdContrato,
        f.NomeFalecido,
        f.Status,
        f.DataHoraPrevista,
        f.Destinacao,
        f.Observacoes,
        [.. EstadosFuneral.Maquina.Transicoes.Where(t => t.De == f.Status).Select(t => t.Para)],
        [.. f.Equipe.OrderBy(m => m.AtribuidoEm).Select(m => new MembroEquipeResponse(m.IdUsuario, m.Nome, m.Funcao))],
        [.. f.Historico.OrderBy(h => h.Em).Select(h => new HistoricoStatusResponse(h.De, h.Para, h.IdUsuario, h.Observacao, h.Em))]);
}

public sealed record MembroEquipeResponse(Guid IdUsuario, string Nome, FuncaoEquipe Funcao);

public sealed record HistoricoStatusResponse(StatusFuneral De, StatusFuneral Para, Guid? IdUsuario, string? Observacao, DateTime Em);

public sealed record FuneralResumoResponse(
    Guid IdFuneral, Guid IdBeneficiario, string NomeFalecido, StatusFuneral Status, DateTime DataHoraPrevista, Destinacao Destinacao);

/// <summary>Leitura da listagem da equipe, com o filtro de tenant do EF Core.</summary>
public interface IFuneraisLeitura
{
    Task<(IReadOnlyList<FuneralResumoResponse> Itens, long Total)> ListarAsync(
        StatusFuneral? status, DateTime? de, DateTime? ate, int deslocamento, int tamanho, CancellationToken cancellationToken);
}

internal sealed class RegistrarBeneficiarioCommandValidator : AbstractValidator<RegistrarBeneficiarioCommand>
{
    public RegistrarBeneficiarioCommandValidator()
    {
        RuleFor(c => c.IdPessoaCoberta).NotEmpty().WithMessage("Informe a pessoa coberta (id_pessoa_coberta) que faleceu.");
        RuleFor(c => c.DataFalecimento).NotNull().WithMessage("Data de falecimento é obrigatória.");
        RuleFor(c => c.LocalFalecimento)
            .NotEmpty().WithMessage("Local do falecimento é obrigatório.")
            .MaximumLength(200).WithMessage("Local do falecimento deve ter no máximo 200 caracteres.");
        RuleFor(c => c.NomeResponsavel)
            .NotEmpty().WithMessage("Nome do responsável pela família é obrigatório.")
            .MaximumLength(150).WithMessage("Nome do responsável deve ter no máximo 150 caracteres.");
        RuleFor(c => c.TelefoneResponsavel)
            .NotEmpty().WithMessage("Telefone do responsável é obrigatório.")
            .MaximumLength(20).WithMessage("Telefone do responsável deve ter no máximo 20 caracteres.");
        RuleFor(c => c.ParentescoResponsavel)
            .NotEmpty().WithMessage("Parentesco do responsável é obrigatório.")
            .MaximumLength(50).WithMessage("Parentesco deve ter no máximo 50 caracteres.");
    }
}

internal sealed class RegistrarFuneralCommandValidator : AbstractValidator<RegistrarFuneralCommand>
{
    public RegistrarFuneralCommandValidator()
    {
        RuleFor(c => c.IdBeneficiario).NotEmpty().WithMessage("Beneficiário é obrigatório.");
        RuleFor(c => c.DataHoraPrevista).NotNull().WithMessage("Data e hora do funeral são obrigatórias.");
        RuleFor(c => c.Destinacao).IsInEnum().WithMessage("Destinação inválida. Use NAO_DEFINIDA, SEPULTAMENTO ou CREMACAO.");
        RuleFor(c => c.Observacoes).MaximumLength(2000).WithMessage("Observações devem ter no máximo 2000 caracteres.");
    }
}

internal sealed class AlterarStatusFuneralCommandValidator : AbstractValidator<AlterarStatusFuneralCommand>
{
    public AlterarStatusFuneralCommandValidator()
    {
        RuleFor(c => c.Status).NotNull().WithMessage("Informe o novo status.")
            .IsInEnum().WithMessage("Status inválido. Use EM_PREPARACAO, VELORIO, CONCLUIDO ou CANCELADO.");
        RuleFor(c => c.Observacao).MaximumLength(1000).WithMessage("Observação deve ter no máximo 1000 caracteres.");
    }
}

internal sealed class AtribuirMembroFuneralCommandValidator : AbstractValidator<AtribuirMembroFuneralCommand>
{
    public AtribuirMembroFuneralCommandValidator() =>
        RuleFor(c => c.Funcao).NotNull().WithMessage("Informe a função.")
            .IsInEnum().WithMessage("Função inválida. Use AGENTE_FUNERARIO, CERIMONIALISTA, MOTORISTA ou OUTRA.");
}

internal sealed class ListarFuneraisQueryValidator : AbstractValidator<ListarFuneraisQuery>
{
    public ListarFuneraisQueryValidator()
    {
        RuleFor(q => q.Status).Must(s => s is null || FiltroStatus.Ler(s) is not null)
            .WithMessage("Status inválido. Use REGISTRADO, EM_PREPARACAO, VELORIO, CONCLUIDO ou CANCELADO.");
        RuleFor(q => q.DataFim).GreaterThanOrEqualTo(q => q.DataInicio).When(q => q.DataInicio is not null && q.DataFim is not null)
            .WithMessage("data_fim não pode ser anterior a data_inicio.");
    }
}

internal static class FiltroStatus
{
    /// <summary>Aceita o formato da API (EM_PREPARACAO) e o nome do enum.</summary>
    public static StatusFuneral? Ler(string? valor) =>
        valor is not null
        && Enum.TryParse(valor.Replace("_", string.Empty, StringComparison.Ordinal), ignoreCase: true, out StatusFuneral lido)
        && Enum.IsDefined(lido)
            ? lido
            : null;
}

public sealed class RegistrarBeneficiarioCommandHandler(
    IBeneficiarioRepository beneficiarios,
    IFuneralUnitOfWork unitOfWork,
    IContratosApi contratos,
    IClientesApi clientes,
    ITenantContext tenantContext,
    TimeProvider timeProvider,
    IAuditoria auditoria)
    : ICommandHandler<RegistrarBeneficiarioCommand, BeneficiarioResponse>
{
    public async ValueTask<Result<BeneficiarioResponse>> Handle(RegistrarBeneficiarioCommand command, CancellationToken cancellationToken)
    {
        Guid idPessoa = command.IdPessoaCoberta!.Value;
        ElegibilidadeDto? elegibilidade = await contratos.VerificarElegibilidadeAsync(idPessoa, cancellationToken);
        if (elegibilidade is null)
        {
            return BeneficiarioErros.PessoaNaoEncontrada;
        }

        if (await beneficiarios.ExisteDaPessoaAsync(idPessoa, cancellationToken))
        {
            return BeneficiarioErros.JaRegistrado;
        }

        if (!elegibilidade.Elegivel)
        {
            return BeneficiarioErros.NaoElegivel(elegibilidade.Motivo!);
        }

        PessoaFalecida? pessoa = await PessoaAsync(elegibilidade, cancellationToken);
        if (pessoa is null)
        {
            return BeneficiarioErros.PessoaNaoEncontrada;
        }

        Result<Beneficiario> registrado = Beneficiario.Registrar(
            tenantContext.RequiredTenantId,
            pessoa,
            command.DataFalecimento!.Value,
            command.LocalFalecimento!,
            new ResponsavelFamilia(command.NomeResponsavel!, command.TelefoneResponsavel!, command.ParentescoResponsavel!),
            DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime));
        if (registrado.IsFailure)
        {
            return Result.Failure<BeneficiarioResponse>(registrado.Error);
        }

        beneficiarios.Adicionar(registrado.Value);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ViolacaoDeUnicidadeException)
        {
            return BeneficiarioErros.JaRegistrado;
        }

        await auditoria.RegistrarAsync(new RegistroAuditoria(
            AcoesAuditadas.BeneficiarioRegistrado, "Beneficiario", registrado.Value.Id, ValoresNovos: new
            {
                id_pessoa_coberta = idPessoa,
                id_contrato = pessoa.IdContrato,
                data_falecimento = registrado.Value.DataFalecimento
            }), cancellationToken);

        return BeneficiarioResponse.De(registrado.Value);
    }

    private async Task<PessoaFalecida?> PessoaAsync(ElegibilidadeDto e, CancellationToken cancellationToken)
    {
        if (e.IdDependente is Guid idDependente)
        {
            DependenteDto? d = (await clientes.ObterDependentesAsync([idDependente], cancellationToken)).FirstOrDefault();
            return d is null ? null : new PessoaFalecida(e.IdPessoaCoberta, e.IdContrato, e.IdCliente, idDependente, d.Nome, d.DataNascimento);
        }

        TitularDto? t = await clientes.ObterTitularAsync(e.IdCliente, cancellationToken);
        return t is null ? null : new PessoaFalecida(e.IdPessoaCoberta, e.IdContrato, e.IdCliente, null, t.Nome, t.DataNascimento);
    }
}

public sealed class ObterBeneficiarioQueryHandler(IBeneficiarioRepository beneficiarios, ICurrentUser usuario)
    : IQueryHandler<ObterBeneficiarioQuery, BeneficiarioResponse>
{
    public async ValueTask<Result<BeneficiarioResponse>> Handle(ObterBeneficiarioQuery query, CancellationToken cancellationToken)
    {
        Beneficiario? beneficiario = await beneficiarios.ObterAsync(query.IdBeneficiario, cancellationToken);
        return beneficiario is not null && AcessoDaFamilia.Permitido(usuario, beneficiario.IdCliente)
            ? BeneficiarioResponse.De(beneficiario)
            : BeneficiarioErros.NaoEncontrado;
    }
}

public sealed class RegistrarFuneralCommandHandler(
    IBeneficiarioRepository beneficiarios,
    IFuneralRepository funerais,
    IFuneralUnitOfWork unitOfWork,
    ICurrentUser usuario,
    TimeProvider timeProvider,
    IAuditoria auditoria)
    : ICommandHandler<RegistrarFuneralCommand, FuneralResponse>
{
    public async ValueTask<Result<FuneralResponse>> Handle(RegistrarFuneralCommand command, CancellationToken cancellationToken)
    {
        Beneficiario? beneficiario = await beneficiarios.ObterAsync(command.IdBeneficiario!.Value, cancellationToken);
        if (beneficiario is null)
        {
            return BeneficiarioErros.NaoEncontrado;
        }

        if (await funerais.ExisteAtivoDoBeneficiarioAsync(beneficiario.Id, cancellationToken))
        {
            return FuneralErros.JaRegistrado;
        }

        Result<FuneralEntity> registrado = FuneralEntity.Registrar(
            beneficiario,
            DateTime.SpecifyKind(command.DataHoraPrevista!.Value.ToUniversalTime(), DateTimeKind.Utc),
            command.Destinacao ?? Destinacao.NaoDefinida,
            command.Observacoes,
            timeProvider.GetUtcNow().UtcDateTime,
            usuario.IdUsuario);
        if (registrado.IsFailure)
        {
            return Result.Failure<FuneralResponse>(registrado.Error);
        }

        funerais.Adicionar(registrado.Value);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ViolacaoDeUnicidadeException)
        {
            return FuneralErros.JaRegistrado;
        }

        await auditoria.RegistrarAsync(new RegistroAuditoria(
            AcoesAuditadas.FuneralRegistrado, "Funeral", registrado.Value.Id, ValoresNovos: new
            {
                id_beneficiario = beneficiario.Id,
                data_hora_prevista = registrado.Value.DataHoraPrevista
            }), cancellationToken);

        return FuneralResponse.De(registrado.Value);
    }
}

public sealed class AlterarStatusFuneralCommandHandler(
    IFuneralRepository funerais,
    IFuneralUnitOfWork unitOfWork,
    ICurrentUser usuario,
    TimeProvider timeProvider,
    IAuditoria auditoria)
    : ICommandHandler<AlterarStatusFuneralCommand, FuneralResponse>
{
    public async ValueTask<Result<FuneralResponse>> Handle(AlterarStatusFuneralCommand command, CancellationToken cancellationToken)
    {
        FuneralEntity? funeral = await funerais.ObterAsync(command.IdFuneral, cancellationToken);
        if (funeral is null)
        {
            return FuneralErros.NaoEncontrado;
        }

        StatusFuneral anterior = funeral.Status;
        Result mudou = funeral.MudarPara(command.Status!.Value, timeProvider.GetUtcNow().UtcDateTime, usuario.IdUsuario, command.Observacao);
        if (mudou.IsFailure)
        {
            return Result.Failure<FuneralResponse>(mudou.Error);
        }

        // Duas mudanças ao mesmo tempo: o xmin recusa a segunda e o handler global responde 409 (ARQ-01).
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await auditoria.RegistrarAsync(new RegistroAuditoria(
            AcoesAuditadas.FuneralStatusAlterado, "Funeral", funeral.Id,
            ValoresAnteriores: new { status = NomeApi.De(anterior) },
            ValoresNovos: new { status = NomeApi.De(funeral.Status), observacao = command.Observacao }), cancellationToken);

        return FuneralResponse.De(funeral);
    }
}

public sealed class ObterFuneralQueryHandler(IFuneralRepository funerais, ICurrentUser usuario) : IQueryHandler<ObterFuneralQuery, FuneralResponse>
{
    public async ValueTask<Result<FuneralResponse>> Handle(ObterFuneralQuery query, CancellationToken cancellationToken)
    {
        FuneralEntity? funeral = await funerais.ObterAsync(query.IdFuneral, cancellationToken);
        return funeral is not null && AcessoDaFamilia.Permitido(usuario, funeral.IdCliente)
            ? FuneralResponse.De(funeral)
            : FuneralErros.NaoEncontrado;
    }
}

public sealed class ListarFuneraisQueryHandler(IFuneraisLeitura leitura) : IQueryHandler<ListarFuneraisQuery, PaginaResult<FuneralResumoResponse>>
{
    public async ValueTask<Result<PaginaResult<FuneralResumoResponse>>> Handle(ListarFuneraisQuery query, CancellationToken cancellationToken)
    {
        PaginaRequest pagina = query.Pagina.Normalizada();

        // O período é de dias inteiros, no horário de Brasília não: as datas da API são UTC (INF-17).
        DateTime? de = query.DataInicio?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        DateTime? ate = query.DataFim?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        (IReadOnlyList<FuneralResumoResponse> itens, long total) = await leitura.ListarAsync(
            FiltroStatus.Ler(query.Status), de, ate, pagina.Deslocamento, pagina.Tamanho, cancellationToken);
        return new PaginaResult<FuneralResumoResponse>(itens, pagina.Pagina, pagina.Tamanho, total);
    }
}

public sealed class AtribuirMembroFuneralCommandHandler(
    IFuneralRepository funerais,
    IFuneralUnitOfWork unitOfWork,
    IIdentidadeApi identidade,
    ITenantContext tenantContext,
    TimeProvider timeProvider)
    : ICommandHandler<AtribuirMembroFuneralCommand, FuneralResponse>
{
    public async ValueTask<Result<FuneralResponse>> Handle(AtribuirMembroFuneralCommand command, CancellationToken cancellationToken)
    {
        FuneralEntity? funeral = await funerais.ObterAsync(command.IdFuneral, cancellationToken);
        if (funeral is null)
        {
            return FuneralErros.NaoEncontrado;
        }

        MembroEquipeDto? membro = await identidade.ObterMembroEquipeAsync(command.IdUsuario, tenantContext.RequiredTenantId, cancellationToken);
        if (membro is null)
        {
            return FuneralErros.MembroInvalido;
        }

        Result atribuido = funeral.AtribuirMembro(membro.IdUsuario, membro.Nome, command.Funcao!.Value, timeProvider.GetUtcNow().UtcDateTime);
        if (atribuido.IsFailure)
        {
            return Result.Failure<FuneralResponse>(atribuido.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return FuneralResponse.De(funeral);
    }
}

public sealed class RemoverMembroFuneralCommandHandler(IFuneralRepository funerais, IFuneralUnitOfWork unitOfWork)
    : ICommandHandler<RemoverMembroFuneralCommand, FuneralResponse>
{
    public async ValueTask<Result<FuneralResponse>> Handle(RemoverMembroFuneralCommand command, CancellationToken cancellationToken)
    {
        FuneralEntity? funeral = await funerais.ObterAsync(command.IdFuneral, cancellationToken);
        if (funeral is null)
        {
            return FuneralErros.NaoEncontrado;
        }

        Result removido = funeral.RemoverMembro(command.IdUsuario);
        if (removido.IsFailure)
        {
            return Result.Failure<FuneralResponse>(removido.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return FuneralResponse.De(funeral);
    }
}

public sealed class MeusFuneraisQueryHandler(IFuneralRepository funerais, ICurrentUser usuario)
    : IQueryHandler<MeusFuneraisQuery, IReadOnlyList<FuneralResumoResponse>>
{
    public async ValueTask<Result<IReadOnlyList<FuneralResumoResponse>>> Handle(MeusFuneraisQuery query, CancellationToken cancellationToken) =>
        (await funerais.ListarAtribuidosAsync(usuario.IdUsuario!.Value, cancellationToken))
            .Select(f => new FuneralResumoResponse(f.Id, f.IdBeneficiario, f.NomeFalecido, f.Status, f.DataHoraPrevista, f.Destinacao))
            .ToList();
}

/// <summary>Publica a mudança de etapa para o Notificacoes avisar a família (FUN-06).</summary>
internal sealed class FuneralStatusAlteradoDomainEventHandler(IFuneralRepository funerais, IClientesApi clientes, IEventBus eventBus)
    : IDomainEventHandler<FuneralStatusAlteradoDomainEvent>
{
    public async Task Handle(FuneralStatusAlteradoDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await funerais.ObterAsync(domainEvent.IdFuneral, cancellationToken) is not FuneralEntity funeral)
        {
            return;
        }

        TitularDto? titular = await clientes.ObterTitularAsync(funeral.IdCliente, cancellationToken);
        await eventBus.PublishAsync(
            new FuneralStatusAlteradoIntegrationEvent(
                funeral.TenantId, funeral.Id, titular?.IdUsuario, funeral.NomeFalecido,
                NomeApi.De(domainEvent.Anterior), NomeApi.De(domainEvent.Atual)),
            cancellationToken);
    }
}

internal static class AcessoDaFamilia
{
    /// <summary>A equipe vê tudo da funerária; o cliente, só o que é do próprio contrato (os outros dão 404, DEC-06).</summary>
    public static bool Permitido(ICurrentUser usuario, Guid idCliente) =>
        usuario.Perfil != Perfis.Cliente || usuario.IdCliente == idCliente;
}
