using FluentValidation;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Funeral.Application.Abstractions;
using Funeraria.Modules.Funeral.Domain.Funerais;
using Funeraria.Modules.Funeral.Domain.Locais;
using FuneralEntity = Funeraria.Modules.Funeral.Domain.Funerais.Funeral;

namespace Funeraria.Modules.Funeral.Application.Locais;

public interface IDadosLocal
{
    string Nome { get; }

    TipoLocal? Tipo { get; }

    string? Endereco { get; }

    bool Proprio { get; }

    int? IntervaloPreparacaoMinutos { get; }
}

/// <summary>Cadastra um local (FUN-05). Nos próprios, o intervalo de preparação padrão é de 60 minutos.</summary>
public sealed record CriarLocalCommand(string Nome, TipoLocal? Tipo, string? Endereco, bool Proprio, int? IntervaloPreparacaoMinutos)
    : ICommand<LocalResponse>, IDadosLocal;

public sealed record AtualizarLocalCommand(Guid IdLocal, string Nome, TipoLocal? Tipo, string? Endereco, bool Proprio, int? IntervaloPreparacaoMinutos)
    : ICommand<LocalResponse>, IDadosLocal;

/// <summary>Local não é excluído (as reservas antigas apontam para ele), só desativado.</summary>
public sealed record AlterarStatusLocalCommand(Guid IdLocal, bool? Ativo) : ICommand;

public sealed record ListarLocaisQuery(bool SomenteAtivos) : IQuery<IReadOnlyList<LocalResponse>>;

/// <summary>Reservas do local num dia (UTC), com o funeral de cada uma (FUN-05).</summary>
public sealed record AgendaLocalQuery(Guid IdLocal, DateOnly Data) : IQuery<AgendaResponse>;

/// <summary>Reserva um local para o funeral. Nos locais próprios, não aceita conflito de horário (FUN-05).</summary>
public sealed record ReservarLocalCommand(Guid IdFuneral, Guid? IdLocal, DateTime? Inicio, DateTime? Fim) : ICommand<ReservaResponse>;

public sealed record CancelarReservaCommand(Guid IdFuneral, Guid IdReserva) : ICommand;

public sealed record LocalResponse(
    Guid IdLocal, string Nome, TipoLocal Tipo, string? Endereco, bool Proprio, int IntervaloPreparacaoMinutos, bool Ativo)
{
    public static LocalResponse De(Local l) => new(l.Id, l.Nome, l.Tipo, l.Endereco, l.Proprio, l.IntervaloPreparacaoMinutos, l.Ativo);
}

public sealed record ReservaResponse(Guid IdReserva, Guid IdLocal, string NomeLocal, TipoLocal TipoLocal, DateTime Inicio, DateTime Fim);

public sealed record AgendaResponse(LocalResponse Local, DateOnly Data, IReadOnlyList<ReservaAgendaResponse> Reservas);

/// <summary>Reserva na agenda. <c>OcupadoAte</c> é o fim mais o intervalo de preparação: o próximo uso só começa depois disso.</summary>
public sealed record ReservaAgendaResponse(Guid IdReserva, Guid IdFuneral, string NomeFalecido, DateTime Inicio, DateTime Fim, DateTime OcupadoAte);

internal abstract class DadosLocalValidator<T> : AbstractValidator<T>
    where T : IDadosLocal
{
    protected DadosLocalValidator()
    {
        RuleFor(l => l.Nome)
            .NotEmpty().WithMessage("Nome do local é obrigatório.")
            .MaximumLength(150).WithMessage("Nome do local deve ter no máximo 150 caracteres.");
        RuleFor(l => l.Tipo).NotNull().WithMessage("Tipo do local é obrigatório.")
            .IsInEnum().WithMessage("Tipo inválido. Use SALA_VELORIO, CEMITERIO, CREMATORIO, IGREJA ou OUTRO.");
        RuleFor(l => l.Endereco).MaximumLength(300).WithMessage("Endereço deve ter no máximo 300 caracteres.");
        RuleFor(l => l.IntervaloPreparacaoMinutos)
            .InclusiveBetween(0, 24 * 60).WithMessage("Intervalo de preparação: de 0 a 1440 minutos.");
    }
}

internal sealed class CriarLocalCommandValidator : DadosLocalValidator<CriarLocalCommand>;

internal sealed class AtualizarLocalCommandValidator : DadosLocalValidator<AtualizarLocalCommand>;

internal sealed class AlterarStatusLocalCommandValidator : AbstractValidator<AlterarStatusLocalCommand>
{
    public AlterarStatusLocalCommandValidator() =>
        RuleFor(c => c.Ativo).NotNull().WithMessage("Informe se o local fica ativo (true) ou não (false).");
}

internal sealed class ReservarLocalCommandValidator : AbstractValidator<ReservarLocalCommand>
{
    public ReservarLocalCommandValidator()
    {
        RuleFor(c => c.IdLocal).NotEmpty().WithMessage("Local é obrigatório.");
        RuleFor(c => c.Inicio).NotNull().WithMessage("Início da reserva é obrigatório.");
        RuleFor(c => c.Fim).NotNull().WithMessage("Fim da reserva é obrigatório.");
    }
}

internal static class DadosLocais
{
    public const int IntervaloPadrao = 60;

    public static DadosLocal De(IDadosLocal d) =>
        new(d.Nome, d.Tipo!.Value, d.Endereco, d.Proprio, d.IntervaloPreparacaoMinutos ?? IntervaloPadrao);

    public static DateTime Utc(DateTime valor) => DateTime.SpecifyKind(valor.ToUniversalTime(), DateTimeKind.Utc);
}

public sealed class CriarLocalCommandHandler(ILocalRepository locais, IFuneralUnitOfWork unitOfWork, ITenantContext tenantContext)
    : ICommandHandler<CriarLocalCommand, LocalResponse>
{
    public async ValueTask<Result<LocalResponse>> Handle(CriarLocalCommand command, CancellationToken cancellationToken)
    {
        var local = Local.Criar(tenantContext.RequiredTenantId, DadosLocais.De(command));
        locais.Adicionar(local);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return LocalResponse.De(local);
    }
}

public sealed class AtualizarLocalCommandHandler(ILocalRepository locais, IFuneralUnitOfWork unitOfWork)
    : ICommandHandler<AtualizarLocalCommand, LocalResponse>
{
    public async ValueTask<Result<LocalResponse>> Handle(AtualizarLocalCommand command, CancellationToken cancellationToken)
    {
        Local? local = await locais.ObterAsync(command.IdLocal, cancellationToken);
        if (local is null)
        {
            return LocalErros.NaoEncontrado;
        }

        // Reservas já feitas guardam o próprio "ocupado até": mudar o intervalo vale para as próximas.
        local.Atualizar(DadosLocais.De(command));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return LocalResponse.De(local);
    }
}

public sealed class AlterarStatusLocalCommandHandler(ILocalRepository locais, IFuneralUnitOfWork unitOfWork)
    : ICommandHandler<AlterarStatusLocalCommand>
{
    public async ValueTask<Result> Handle(AlterarStatusLocalCommand command, CancellationToken cancellationToken)
    {
        Local? local = await locais.ObterAsync(command.IdLocal, cancellationToken);
        if (local is null)
        {
            return LocalErros.NaoEncontrado;
        }

        local.AlterarStatus(command.Ativo!.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class ListarLocaisQueryHandler(ILocalRepository locais) : IQueryHandler<ListarLocaisQuery, IReadOnlyList<LocalResponse>>
{
    public async ValueTask<Result<IReadOnlyList<LocalResponse>>> Handle(ListarLocaisQuery query, CancellationToken cancellationToken) =>
        (await locais.ListarAsync(query.SomenteAtivos, cancellationToken)).Select(LocalResponse.De).ToList();
}

public sealed class AgendaLocalQueryHandler(ILocalRepository locais, IReservaRepository reservas, IFuneralRepository funerais)
    : IQueryHandler<AgendaLocalQuery, AgendaResponse>
{
    public async ValueTask<Result<AgendaResponse>> Handle(AgendaLocalQuery query, CancellationToken cancellationToken)
    {
        Local? local = await locais.ObterAsync(query.IdLocal, cancellationToken);
        if (local is null)
        {
            return LocalErros.NaoEncontrado;
        }

        DateTime de = query.Data.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var itens = new List<ReservaAgendaResponse>();
        foreach (Reserva r in await reservas.ListarDoLocalAsync(local.Id, de, de.AddDays(1), cancellationToken))
        {
            FuneralEntity? funeral = await funerais.ObterAsync(r.IdFuneral, cancellationToken);
            itens.Add(new ReservaAgendaResponse(r.Id, r.IdFuneral, funeral?.NomeFalecido ?? string.Empty, r.Inicio, r.Fim, r.OcupadoAte));
        }

        return new AgendaResponse(LocalResponse.De(local), query.Data, itens);
    }
}

public sealed class ReservarLocalCommandHandler(
    ILocalRepository locais,
    IReservaRepository reservas,
    IFuneralRepository funerais,
    IFuneralUnitOfWork unitOfWork)
    : ICommandHandler<ReservarLocalCommand, ReservaResponse>
{
    public async ValueTask<Result<ReservaResponse>> Handle(ReservarLocalCommand command, CancellationToken cancellationToken)
    {
        FuneralEntity? funeral = await funerais.ObterAsync(command.IdFuneral, cancellationToken);
        if (funeral is null)
        {
            return FuneralErros.NaoEncontrado;
        }

        if (funeral.Encerrado)
        {
            return FuneralErros.Encerrado;
        }

        Local? local = await locais.ObterAsync(command.IdLocal!.Value, cancellationToken);
        if (local is null)
        {
            return LocalErros.NaoEncontrado;
        }

        Result<Reserva> reserva = local.Reservar(funeral.Id, DadosLocais.Utc(command.Inicio!.Value), DadosLocais.Utc(command.Fim!.Value));
        if (reserva.IsFailure)
        {
            return Result.Failure<ReservaResponse>(reserva.Error);
        }

        if (reserva.Value.ControlaAgenda
            && (await reservas.ListarDoLocalAsync(local.Id, reserva.Value.Inicio, reserva.Value.OcupadoAte, cancellationToken))
                .Any(r => r.Conflita(reserva.Value)))
        {
            return LocalErros.HorarioOcupado;
        }

        reservas.Adicionar(reserva.Value);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ViolacaoDeUnicidadeException)
        {
            // Duas reservas ao mesmo tempo: a restrição de exclusão do banco recusa a segunda.
            return LocalErros.HorarioOcupado;
        }

        return new ReservaResponse(reserva.Value.Id, local.Id, local.Nome, local.Tipo, reserva.Value.Inicio, reserva.Value.Fim);
    }
}

public sealed class CancelarReservaCommandHandler(IReservaRepository reservas, IFuneralUnitOfWork unitOfWork) : ICommandHandler<CancelarReservaCommand>
{
    public async ValueTask<Result> Handle(CancelarReservaCommand command, CancellationToken cancellationToken)
    {
        Reserva? reserva = await reservas.ObterAsync(command.IdReserva, cancellationToken);
        if (reserva is null || reserva.IdFuneral != command.IdFuneral || reserva.Cancelada)
        {
            return LocalErros.ReservaNaoEncontrada;
        }

        reserva.Cancelar();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>Reservas ativas do funeral com o nome do local, para o detalhe (FUN-07).</summary>
public sealed class ReservasDoFuneral(IReservaRepository reservas, ILocalRepository locais)
{
    public async Task<IReadOnlyList<ReservaResponse>> ListarAsync(Guid idFuneral, CancellationToken cancellationToken)
    {
        var lista = new List<ReservaResponse>();
        foreach (Reserva r in await reservas.ListarDoFuneralAsync(idFuneral, cancellationToken))
        {
            Local? local = await locais.ObterAsync(r.IdLocal, cancellationToken);
            lista.Add(new ReservaResponse(r.Id, r.IdLocal, local?.Nome ?? string.Empty, local?.Tipo ?? TipoLocal.Outro, r.Inicio, r.Fim));
        }

        return lista;
    }
}

/// <summary>Funeral cancelado libera os locais reservados para ele (FUN-05).</summary>
internal sealed class LiberarLocaisDoFuneralCancelado(IReservaRepository reservas, IFuneralUnitOfWork unitOfWork)
    : IDomainEventHandler<FuneralStatusAlteradoDomainEvent>
{
    public async Task Handle(FuneralStatusAlteradoDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (domainEvent.Atual != StatusFuneral.Cancelado)
        {
            return;
        }

        foreach (Reserva reserva in await reservas.ListarDoFuneralAsync(domainEvent.IdFuneral, cancellationToken))
        {
            reserva.Cancelar();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
