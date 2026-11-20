using FluentValidation;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Contratos.Contracts;
using Funeraria.Modules.Documentos.Contracts;
using Funeraria.Modules.Funeral.Contracts;
using Funeraria.Modules.Funeral.Contracts.IntegrationEvents;
using Funeraria.Modules.Planos.Contracts;
using Funeraria.Modules.Terceiros.Contracts;
using Funeraria.Modules.Terceiros.Contracts.IntegrationEvents;
using Funeraria.Modules.Translado.Domain.Translados;
using TransladoEntity = Funeraria.Modules.Translado.Domain.Translados.Translado;

namespace Funeraria.Modules.Translado.Application.Translados;

public interface ITransladoUnitOfWork : IUnitOfWork;

/// <summary>Distância por rota entre dois endereços (DEC-10). Nula quando não encontra o endereço ou a API falha: o funcionário informa os km.</summary>
public interface IDistanciaGateway
{
    Task<decimal?> CalcularKmAsync(Endereco origem, Endereco destino, CancellationToken cancellationToken);
}

/// <summary>Entrega em tempo real para quem acompanha o translado (TRA-06).</summary>
public interface IEntregaStatusTranslado
{
    Task EnviarAsync(Guid idTranslado, StatusTranslado status, DateTime dataHora, string? observacao, CancellationToken cancellationToken);
}

public sealed record EnderecoRequest(string? Endereco, string? Cidade, string? Uf, string? Pais)
{
    public Endereco ParaDominio() => new(Endereco!.Trim(), Cidade!.Trim(), Uf!.Trim().ToUpperInvariant(), string.IsNullOrWhiteSpace(Pais) ? "BR" : Pais.Trim().ToUpperInvariant());
}

/// <summary>Registra o translado do funeral (TRA-01). Sem <c>DistanciaKm</c>, a distância vem da API (TRA-02).</summary>
public sealed record RegistrarTransladoCommand(Guid IdFuneral, EnderecoRequest? Origem, EnderecoRequest? Destino, DateTime? DataPrevista, decimal? DistanciaKm)
    : ICommand<TransladoResponse>;

/// <summary>Preço de um translado antes de registrar (TRA-02). Com o funeral informado, desconta os km do plano.</summary>
public sealed record SimularTransladoCommand(EnderecoRequest? Origem, EnderecoRequest? Destino, decimal? DistanciaKm, Guid? IdFuneral) : IQuery<SimulacaoResponse>;

public sealed record ObterTransladoQuery(Guid IdTranslado) : IQuery<TransladoResponse>;

public sealed record TransladosDoFuneralQuery(Guid IdFuneral) : IQuery<IReadOnlyList<TransladoResponse>>;

public sealed record HistoricoTransladoQuery(Guid IdTranslado) : IQuery<IReadOnlyList<HistoricoTransladoResponse>>;

public sealed record ObterConfiguracaoTransladoQuery : IQuery<ConfiguracaoTransladoResponse>;

public sealed record DefinirTabelaPrecosCommand(IReadOnlyList<FaixaKm>? Faixas) : ICommand<ConfiguracaoTransladoResponse>;

/// <summary>Documentos exigidos por tipo de translado (TRA-03), com os tipos do CER-01.</summary>
public sealed record DefinirDocumentosExigidosCommand(IReadOnlyDictionary<TipoTranslado, IReadOnlyList<string>>? Documentos) : ICommand<ConfiguracaoTransladoResponse>;

/// <summary>Documentos do tipo deste translado e o que falta (TRA-03).</summary>
public sealed record PendenciasDocumentosQuery(Guid IdTranslado) : IQuery<PendenciasDocumentosResponse>;

/// <summary>Empresas de frota ativas que atendem a origem do translado (TRA-04).</summary>
public sealed record EmpresasDisponiveisQuery(Guid IdTranslado) : IQuery<IReadOnlyList<EmpresaDisponivelResponse>>;

public sealed record AtribuirFrotaCommand(Guid IdTranslado, Guid? IdTerceiro) : ICommand<TransladoResponse>;

/// <summary>Muda o status pela equipe (TRA-05).</summary>
public sealed record AlterarStatusTransladoCommand(Guid IdTranslado, StatusTranslado? Status, string? Observacao) : ICommand<TransladoResponse>;

/// <summary>Muda o status pela empresa atribuída, no portal (TRA-05).</summary>
public sealed record AlterarStatusPelaFrotaCommand(Guid IdTranslado, StatusTranslado? Status, string? Observacao) : ICommand<TransladoFrotaResponse>;

public sealed record InformarMotoristaCommand(Guid IdTranslado, string? Nome, string? Telefone, string? Placa, string? Modelo) : ICommand<TransladoFrotaResponse>;

public sealed record MeusTransladosQuery : IQuery<IReadOnlyList<TransladoFrotaResponse>>;

public sealed record ObterTransladoFrotaQuery(Guid IdTranslado) : IQuery<TransladoFrotaResponse>;

public sealed record BaixarDocumentoFrotaQuery(Guid IdTranslado, Guid IdDocumento) : IQuery<LinkDocumentoDto>;

public sealed record EnderecoResponse(string Endereco, string Cidade, string Uf, string Pais)
{
    public static EnderecoResponse De(Endereco e) => new(e.Logradouro, e.Cidade, e.Uf, e.Pais);
}

public sealed record MotoristaResponse(string Nome, string Telefone, string Placa, string Modelo);

public sealed record TransladoResponse(
    Guid IdTranslado,
    Guid IdFuneral,
    TipoTranslado Tipo,
    StatusTranslado Status,
    EnderecoResponse Origem,
    EnderecoResponse Destino,
    DateTime DataPrevista,
    decimal DistanciaKm,
    bool DistanciaManual,
    decimal Valor,
    decimal ValorCobertoPlano,
    decimal ValorAPagar,
    Guid? IdTerceiroFrota,
    string? NomeFrota,
    StatusAceite? StatusAceite,
    string? MotivoRecusa,
    MotoristaResponse? Motorista)
{
    public static TransladoResponse De(TransladoEntity t) => new(
        t.Id, t.IdFuneral, t.Tipo, t.Status, EnderecoResponse.De(t.Origem), EnderecoResponse.De(t.Destino), t.DataPrevista, t.DistanciaKm, t.DistanciaManual,
        t.Valor, t.ValorCobertoPlano, t.Valor - t.ValorCobertoPlano, t.IdTerceiroFrota, t.NomeFrota, t.StatusAceite, t.MotivoRecusa, MotoristaDe(t));

    internal static MotoristaResponse? MotoristaDe(TransladoEntity t) =>
        t.MotoristaNome is null ? null : new MotoristaResponse(t.MotoristaNome, t.MotoristaTelefone!, t.VeiculoPlaca!, t.VeiculoModelo!);
}

/// <summary>O que a empresa vê (TRA-04, LGPD): sem CPF, contrato nem valores.</summary>
public sealed record TransladoFrotaResponse(
    Guid IdTranslado,
    string NomeFalecido,
    StatusTranslado Status,
    EnderecoResponse Origem,
    EnderecoResponse Destino,
    DateTime DataPrevista,
    string ContatoNome,
    string ContatoTelefone,
    StatusAceite? StatusAceite,
    MotoristaResponse? Motorista)
{
    public static TransladoFrotaResponse De(TransladoEntity t) => new(
        t.Id, t.NomeFalecido, t.Status, EnderecoResponse.De(t.Origem), EnderecoResponse.De(t.Destino), t.DataPrevista, t.ContatoNome, t.ContatoTelefone,
        t.StatusAceite, TransladoResponse.MotoristaDe(t));
}

public sealed record SimulacaoResponse(TipoTranslado Tipo, decimal DistanciaKm, bool DistanciaManual, decimal Valor, decimal ValorCobertoPlano, decimal ValorAPagar);

public sealed record HistoricoTransladoResponse(StatusTranslado De, StatusTranslado Para, Guid? IdUsuario, string? Observacao, DateTime Em);

public sealed record ConfiguracaoTransladoResponse(IReadOnlyList<FaixaKm> Faixas, IReadOnlyDictionary<TipoTranslado, IReadOnlyList<string>> DocumentosExigidos);

public sealed record DocumentoExigidoTransladoResponse(string Tipo, bool Presente);

public sealed record PendenciasDocumentosResponse(TipoTranslado Tipo, IReadOnlyList<DocumentoExigidoTransladoResponse> Documentos, bool Completo);

public sealed record EmpresaDisponivelResponse(Guid IdTerceiro, string Nome);

internal static class RegrasEndereco
{
    public static void Aplicar<T>(IRuleBuilderInitial<T, EnderecoRequest?> regra, string nome) =>
        regra.Cascade(CascadeMode.Stop)
            .NotNull().WithMessage($"Informe o endereço de {nome}.")
            .Must(e => !string.IsNullOrWhiteSpace(e!.Endereco) && !string.IsNullOrWhiteSpace(e.Cidade) && !string.IsNullOrWhiteSpace(e.Uf))
            .WithMessage($"Informe endereço, cidade e UF de {nome}.")
            .Must(e => e!.Endereco!.Length <= 200 && e.Cidade!.Length <= 100 && e.Uf!.Trim().Length == 2 && (e.Pais is null || e.Pais.Length <= 2))
            .WithMessage($"Endereço de {nome} inválido: até 200 caracteres, cidade até 100, UF e país com 2 letras.");
}

internal sealed class RegistrarTransladoCommandValidator : AbstractValidator<RegistrarTransladoCommand>
{
    public RegistrarTransladoCommandValidator()
    {
        RegrasEndereco.Aplicar(RuleFor(c => c.Origem), "origem");
        RegrasEndereco.Aplicar(RuleFor(c => c.Destino), "destino");
        RuleFor(c => c.DataPrevista).NotNull().WithMessage("Data prevista é obrigatória.");
        RuleFor(c => c.DistanciaKm).GreaterThan(0).WithMessage("A distância deve ser maior que zero.");
    }
}

internal sealed class SimularTransladoCommandValidator : AbstractValidator<SimularTransladoCommand>
{
    public SimularTransladoCommandValidator()
    {
        RegrasEndereco.Aplicar(RuleFor(c => c.Origem), "origem");
        RegrasEndereco.Aplicar(RuleFor(c => c.Destino), "destino");
        RuleFor(c => c.DistanciaKm).GreaterThan(0).WithMessage("A distância deve ser maior que zero.");
    }
}

internal sealed class DefinirTabelaPrecosCommandValidator : AbstractValidator<DefinirTabelaPrecosCommand>
{
    public DefinirTabelaPrecosCommandValidator() => RuleFor(c => c.Faixas).NotEmpty().WithMessage("Informe as faixas da tabela.");
}

internal sealed class DefinirDocumentosExigidosCommandValidator : AbstractValidator<DefinirDocumentosExigidosCommand>
{
    public DefinirDocumentosExigidosCommandValidator() => RuleFor(c => c.Documentos).NotNull().WithMessage("Informe os documentos por tipo de translado.");
}

internal sealed class AtribuirFrotaCommandValidator : AbstractValidator<AtribuirFrotaCommand>
{
    public AtribuirFrotaCommandValidator() => RuleFor(c => c.IdTerceiro).NotEmpty().WithMessage("Informe a empresa de translado (id_terceiro).");
}

internal sealed class AlterarStatusTransladoCommandValidator : AbstractValidator<AlterarStatusTransladoCommand>
{
    public AlterarStatusTransladoCommandValidator() => RuleFor(c => c.Status).NotNull().WithMessage("Informe o novo status.");
}

internal sealed class AlterarStatusPelaFrotaCommandValidator : AbstractValidator<AlterarStatusPelaFrotaCommand>
{
    public AlterarStatusPelaFrotaCommandValidator() => RuleFor(c => c.Status).NotNull().WithMessage("Informe o novo status.");
}

internal sealed class InformarMotoristaCommandValidator : AbstractValidator<InformarMotoristaCommand>
{
    public InformarMotoristaCommandValidator()
    {
        RuleFor(c => c.Nome).NotEmpty().WithMessage("Nome do motorista é obrigatório.").MaximumLength(150);
        RuleFor(c => c.Telefone).NotEmpty().WithMessage("Telefone do motorista é obrigatório.").MaximumLength(20);
        RuleFor(c => c.Placa).NotEmpty().WithMessage("Placa do veículo é obrigatória.").MaximumLength(10);
        RuleFor(c => c.Modelo).NotEmpty().WithMessage("Modelo do veículo é obrigatório.").MaximumLength(100);
    }
}

/// <summary>Calcula o preço: distância informada ou pela API (sem repetir a consulta), tabela da funerária e km do plano (TRA-02).</summary>
public sealed class CotacaoDeTranslado(
    ITransladoRepository translados,
    ITransladoUnitOfWork unitOfWork,
    IDistanciaGateway distancias,
    IContratosApi contratos,
    IPlanosApi planos)
{
    public async Task<Result<(Cotacao Cotacao, bool Manual)>> CotarAsync(Endereco origem, Endereco destino, decimal? informada, Guid? idContrato, CancellationToken cancellationToken)
    {
        ConfiguracaoTranslado? configuracao = await translados.ObterConfiguracaoAsync(cancellationToken);
        if (configuracao is null || configuracao.Faixas.Count == 0)
        {
            return TransladoErros.TabelaNaoConfigurada;
        }

        decimal? km = informada ?? await DistanciaPelaApiAsync(origem, destino, cancellationToken);
        if (km is null)
        {
            return TransladoErros.DistanciaObrigatoria;
        }

        return (TabelaKm.Cotar(configuracao.Faixas, km.Value, await KmDoPlanoAsync(idContrato, cancellationToken)), informada is not null);
    }

    private async Task<decimal?> DistanciaPelaApiAsync(Endereco origem, Endereco destino, CancellationToken cancellationToken)
    {
        string chave = DistanciaCalculada.ChaveDe(origem, destino);
        if (await translados.ObterDistanciaAsync(chave, cancellationToken) is decimal guardada)
        {
            return guardada;
        }

        decimal? km = await distancias.CalcularKmAsync(origem, destino, cancellationToken);
        if (km is decimal calculada)
        {
            translados.AdicionarDistancia(DistanciaCalculada.Criar(chave, calculada));
            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (ViolacaoDeUnicidadeException)
            {
                // Duas consultas iguais ao mesmo tempo: a distância já ficou guardada pela outra.
            }
        }

        return km;
    }

    private async Task<int> KmDoPlanoAsync(Guid? idContrato, CancellationToken cancellationToken)
    {
        if (idContrato is not Guid id || await contratos.PlanoDoContratoAsync(id, cancellationToken) is not Guid plano)
        {
            return 0;
        }

        return (await planos.ObterCoberturasAsync(plano, cancellationToken)).Max(c => c.KmTransladoIncluso) ?? 0;
    }
}

public sealed class RegistrarTransladoCommandHandler(
    ITransladoRepository translados,
    ITransladoUnitOfWork unitOfWork,
    IFuneralApi funerais,
    CotacaoDeTranslado cotacao,
    ITenantContext tenantContext,
    ICurrentUser usuario,
    TimeProvider timeProvider,
    IAuditoria auditoria)
    : ICommandHandler<RegistrarTransladoCommand, TransladoResponse>
{
    public async ValueTask<Result<TransladoResponse>> Handle(RegistrarTransladoCommand command, CancellationToken cancellationToken)
    {
        FuneralResumoDto? funeral = await funerais.ObterFuneralAsync(command.IdFuneral, cancellationToken);
        if (funeral is null)
        {
            return TransladoErros.FuneralNaoEncontrado;
        }

        if (funeral.Encerrado)
        {
            return TransladoErros.FuneralEncerrado;
        }

        Endereco origem = command.Origem!.ParaDominio();
        Endereco destino = command.Destino!.ParaDominio();

        // Antes da distância: translado internacional não chega a consultar a API.
        Result<TipoTranslado> tipo = TiposTranslado.Calcular(origem, destino);
        if (tipo.IsFailure)
        {
            return Result.Failure<TransladoResponse>(tipo.Error);
        }

        var cotada = await cotacao.CotarAsync(origem, destino, command.DistanciaKm, funeral.IdContrato, cancellationToken);
        if (cotada.IsFailure)
        {
            return Result.Failure<TransladoResponse>(cotada.Error);
        }

        Result<TransladoEntity> registrado = TransladoEntity.Registrar(
            tenantContext.RequiredTenantId,
            new DadosFuneral(funeral.IdFuneral, funeral.IdBeneficiario, funeral.IdCliente, funeral.NomeFalecido, funeral.NomeResponsavel, funeral.TelefoneResponsavel),
            origem,
            destino,
            DateTime.SpecifyKind(command.DataPrevista!.Value.ToUniversalTime(), DateTimeKind.Utc),
            cotada.Value.Cotacao,
            cotada.Value.Manual,
            timeProvider.GetUtcNow().UtcDateTime,
            usuario.IdUsuario);
        if (registrado.IsFailure)
        {
            return Result.Failure<TransladoResponse>(registrado.Error);
        }

        translados.Adicionar(registrado.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await auditoria.RegistrarAsync(new RegistroAuditoria(
            AcoesAuditadas.TransladoRegistrado, "Translado", registrado.Value.Id,
            ValoresNovos: new { id_funeral = funeral.IdFuneral, tipo = registrado.Value.Tipo.ToString(), valor = registrado.Value.Valor }), cancellationToken);
        return TransladoResponse.De(registrado.Value);
    }
}

public sealed class SimularTransladoCommandHandler(IFuneralApi funerais, CotacaoDeTranslado cotacao) : IQueryHandler<SimularTransladoCommand, SimulacaoResponse>
{
    public async ValueTask<Result<SimulacaoResponse>> Handle(SimularTransladoCommand query, CancellationToken cancellationToken)
    {
        Endereco origem = query.Origem!.ParaDominio();
        Endereco destino = query.Destino!.ParaDominio();
        Result<TipoTranslado> tipo = TiposTranslado.Calcular(origem, destino);
        if (tipo.IsFailure)
        {
            return Result.Failure<SimulacaoResponse>(tipo.Error);
        }

        Guid? idContrato = null;
        if (query.IdFuneral is Guid idFuneral)
        {
            FuneralResumoDto? funeral = await funerais.ObterFuneralAsync(idFuneral, cancellationToken);
            if (funeral is null)
            {
                return TransladoErros.FuneralNaoEncontrado;
            }

            idContrato = funeral.IdContrato;
        }

        var cotada = await cotacao.CotarAsync(origem, destino, query.DistanciaKm, idContrato, cancellationToken);
        if (cotada.IsFailure)
        {
            return Result.Failure<SimulacaoResponse>(cotada.Error);
        }

        Cotacao c = cotada.Value.Cotacao;
        return new SimulacaoResponse(tipo.Value, c.DistanciaKm, cotada.Value.Manual, c.Valor, c.ValorCoberto, c.ValorAPagar);
    }
}

/// <summary>A equipe vê qualquer translado da funerária; o cliente, só os do próprio contrato (os outros dão 404).</summary>
public sealed class AcessoAoTranslado(ITransladoRepository translados)
{
    public async Task<TransladoEntity?> ObterAsync(Guid idTranslado, ICurrentUser usuario, CancellationToken cancellationToken)
    {
        TransladoEntity? translado = await translados.ObterAsync(idTranslado, cancellationToken);
        return translado is not null && (usuario.Perfil != Perfis.Cliente || translado.IdCliente == usuario.IdCliente) ? translado : null;
    }

    /// <summary>Só a empresa atribuída; a de outra responde 404 (TER-02).</summary>
    public async Task<TransladoEntity?> ObterDaFrotaAsync(Guid idTranslado, Guid? idTerceiro, CancellationToken cancellationToken)
    {
        TransladoEntity? translado = await translados.ObterAsync(idTranslado, cancellationToken);
        return translado is not null && idTerceiro is not null && translado.IdTerceiroFrota == idTerceiro ? translado : null;
    }

    /// <summary>Quem pode acompanhar pelo hub (TRA-06): equipe da funerária, titular do contrato ou a empresa atribuída.</summary>
    public async Task<bool> PodeAcompanharAsync(Guid idTranslado, string? perfil, Guid? idCliente, Guid? idTerceiro, CancellationToken cancellationToken)
    {
        TransladoEntity? translado = await translados.ObterAsync(idTranslado, cancellationToken);
        return translado is not null && perfil switch
        {
            Perfis.Funeraria or Perfis.Funcionario or Perfis.Consultor => true,
            Perfis.Cliente => idCliente is not null && translado.IdCliente == idCliente,
            Perfis.Terceiro => idTerceiro is not null && translado.IdTerceiroFrota == idTerceiro,
            _ => false
        };
    }
}

public sealed class ObterTransladoQueryHandler(AcessoAoTranslado acesso, ICurrentUser usuario) : IQueryHandler<ObterTransladoQuery, TransladoResponse>
{
    public async ValueTask<Result<TransladoResponse>> Handle(ObterTransladoQuery query, CancellationToken cancellationToken) =>
        await acesso.ObterAsync(query.IdTranslado, usuario, cancellationToken) is TransladoEntity t ? TransladoResponse.De(t) : TransladoErros.NaoEncontrado;
}

public sealed class TransladosDoFuneralQueryHandler(ITransladoRepository translados, IFuneralApi funerais, ICurrentUser usuario)
    : IQueryHandler<TransladosDoFuneralQuery, IReadOnlyList<TransladoResponse>>
{
    public async ValueTask<Result<IReadOnlyList<TransladoResponse>>> Handle(TransladosDoFuneralQuery query, CancellationToken cancellationToken)
    {
        FuneralResumoDto? funeral = await funerais.ObterFuneralAsync(query.IdFuneral, cancellationToken);
        if (funeral is null || (usuario.Perfil == Perfis.Cliente && funeral.IdCliente != usuario.IdCliente))
        {
            return TransladoErros.FuneralNaoEncontrado;
        }

        return (await translados.ListarDoFuneralAsync(query.IdFuneral, cancellationToken)).Select(TransladoResponse.De).ToList();
    }
}

public sealed class HistoricoTransladoQueryHandler(AcessoAoTranslado acesso, ICurrentUser usuario)
    : IQueryHandler<HistoricoTransladoQuery, IReadOnlyList<HistoricoTransladoResponse>>
{
    public async ValueTask<Result<IReadOnlyList<HistoricoTransladoResponse>>> Handle(HistoricoTransladoQuery query, CancellationToken cancellationToken) =>
        await acesso.ObterAsync(query.IdTranslado, usuario, cancellationToken) is TransladoEntity t
            ? t.Historico.OrderBy(h => h.Em).Select(h => new HistoricoTransladoResponse(h.De, h.Para, h.IdUsuario, h.Observacao, h.Em)).ToList()
            : TransladoErros.NaoEncontrado;
}

public sealed class ObterConfiguracaoTransladoQueryHandler(ITransladoRepository translados) : IQueryHandler<ObterConfiguracaoTransladoQuery, ConfiguracaoTransladoResponse>
{
    public async ValueTask<Result<ConfiguracaoTransladoResponse>> Handle(ObterConfiguracaoTransladoQuery query, CancellationToken cancellationToken)
    {
        ConfiguracaoTranslado? c = await translados.ObterConfiguracaoAsync(cancellationToken);
        return new ConfiguracaoTransladoResponse(c?.Faixas ?? [], c?.DocumentosExigidos ?? ConfiguracaoTranslado.PadraoDocumentos);
    }
}

public sealed class DefinirTabelaPrecosCommandHandler(ITransladoRepository translados, ITransladoUnitOfWork unitOfWork, ITenantContext tenantContext)
    : ICommandHandler<DefinirTabelaPrecosCommand, ConfiguracaoTransladoResponse>
{
    public async ValueTask<Result<ConfiguracaoTransladoResponse>> Handle(DefinirTabelaPrecosCommand command, CancellationToken cancellationToken)
    {
        ConfiguracaoTranslado? configuracao = await translados.ObterConfiguracaoAsync(cancellationToken);
        bool nova = configuracao is null;
        configuracao ??= ConfiguracaoTranslado.Criar(tenantContext.RequiredTenantId);
        Result definida = configuracao.DefinirFaixas(command.Faixas!);
        if (definida.IsFailure)
        {
            return Result.Failure<ConfiguracaoTransladoResponse>(definida.Error);
        }

        if (nova)
        {
            translados.AdicionarConfiguracao(configuracao);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ConfiguracaoTransladoResponse(configuracao.Faixas, configuracao.DocumentosExigidos);
    }
}

public sealed class DefinirDocumentosExigidosCommandHandler(ITransladoRepository translados, ITransladoUnitOfWork unitOfWork, ITenantContext tenantContext)
    : ICommandHandler<DefinirDocumentosExigidosCommand, ConfiguracaoTransladoResponse>
{
    public async ValueTask<Result<ConfiguracaoTransladoResponse>> Handle(DefinirDocumentosExigidosCommand command, CancellationToken cancellationToken)
    {
        ConfiguracaoTranslado? configuracao = await translados.ObterConfiguracaoAsync(cancellationToken);
        if (configuracao is null)
        {
            configuracao = ConfiguracaoTranslado.Criar(tenantContext.RequiredTenantId);
            translados.AdicionarConfiguracao(configuracao);
        }

        configuracao.DefinirDocumentos(command.Documentos!);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ConfiguracaoTransladoResponse(configuracao.Faixas, configuracao.DocumentosExigidos);
    }
}

/// <summary>Confere os documentos exigidos pelo tipo do translado (TRA-03).</summary>
public sealed class DocumentosDoTranslado(ITransladoRepository translados, IDocumentosApi documentos)
{
    public async Task<PendenciasDocumentosResponse> PendenciasAsync(TransladoEntity translado, CancellationToken cancellationToken)
    {
        ConfiguracaoTranslado? configuracao = await translados.ObterConfiguracaoAsync(cancellationToken);
        IReadOnlyList<string> exigidos = configuracao?.DocumentosDe(translado.Tipo) ?? ConfiguracaoTranslado.PadraoDocumentos[translado.Tipo];
        HashSet<string> anexados = [.. (await documentos.ListarDoBeneficiarioAsync(translado.IdBeneficiario, cancellationToken)).Select(d => d.Tipo)];
        DocumentoExigidoTransladoResponse[] lista = [.. exigidos.Select(t => new DocumentoExigidoTransladoResponse(t, anexados.Contains(t)))];
        return new PendenciasDocumentosResponse(translado.Tipo, lista, lista.All(d => d.Presente));
    }
}

public sealed class PendenciasDocumentosQueryHandler(AcessoAoTranslado acesso, DocumentosDoTranslado documentos, ICurrentUser usuario)
    : IQueryHandler<PendenciasDocumentosQuery, PendenciasDocumentosResponse>
{
    public async ValueTask<Result<PendenciasDocumentosResponse>> Handle(PendenciasDocumentosQuery query, CancellationToken cancellationToken) =>
        await acesso.ObterAsync(query.IdTranslado, usuario, cancellationToken) is TransladoEntity t
            ? await documentos.PendenciasAsync(t, cancellationToken)
            : TransladoErros.NaoEncontrado;
}

public sealed class EmpresasDisponiveisQueryHandler(ITransladoRepository translados, ITerceirosConsultas terceiros)
    : IQueryHandler<EmpresasDisponiveisQuery, IReadOnlyList<EmpresaDisponivelResponse>>
{
    public async ValueTask<Result<IReadOnlyList<EmpresaDisponivelResponse>>> Handle(EmpresasDisponiveisQuery query, CancellationToken cancellationToken)
    {
        if (await translados.ObterAsync(query.IdTranslado, cancellationToken) is not TransladoEntity t)
        {
            return TransladoErros.NaoEncontrado;
        }

        return (await terceiros.ListarFrotasDaRegiaoAsync(t.Origem.Cidade, t.Origem.Uf, cancellationToken))
            .Select(e => new EmpresaDisponivelResponse(e.IdTerceiro, e.NomeRazaoSocial)).ToList();
    }
}

public sealed class AtribuirFrotaCommandHandler(
    ITransladoRepository translados,
    ITransladoUnitOfWork unitOfWork,
    ITerceirosConsultas terceiros,
    ITerceirosAtribuicoes atribuicoes,
    IDocumentosApi documentos)
    : ICommandHandler<AtribuirFrotaCommand, TransladoResponse>
{
    public async ValueTask<Result<TransladoResponse>> Handle(AtribuirFrotaCommand command, CancellationToken cancellationToken)
    {
        if (await translados.ObterAsync(command.IdTranslado, cancellationToken) is not TransladoEntity t)
        {
            return TransladoErros.NaoEncontrado;
        }

        TerceiroResumoDto? empresa = (await terceiros.ListarFrotasDaRegiaoAsync(t.Origem.Cidade, t.Origem.Uf, cancellationToken))
            .FirstOrDefault(e => e.IdTerceiro == command.IdTerceiro);
        if (empresa is null)
        {
            return TransladoErros.FrotaInvalida;
        }

        Result<Guid?> anterior = t.AtribuirFrota(empresa.IdTerceiro, empresa.NomeRazaoSocial);
        if (anterior.IsFailure)
        {
            return Result.Failure<TransladoResponse>(anterior.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (anterior.Value is Guid idAnterior)
        {
            await atribuicoes.EncerrarAsync(idAnterior, concluida: false, cancellationToken);
        }

        // A empresa vê só origem, destino, horário, contato do responsável e documentos do translado (TRA-04).
        DocumentoAtribuicao[] docs = [.. (await documentos.ListarDoBeneficiarioAsync(t.IdBeneficiario, cancellationToken))
            .Where(d => d.Tipo != "CERTIDAO_OBITO" || t.Tipo == TipoTranslado.Interestadual)
            .Select(d => new DocumentoAtribuicao(d.Descricao, d.IdDocumento))];
        Result<Guid> atribuicao = await atribuicoes.AtribuirAsync(
            new NovaAtribuicao(
                empresa.IdTerceiro,
                t.Id,
                new ResumoTranslado(t.NomeFalecido, t.Origem.Resumo, t.Destino.Resumo, t.DataPrevista, new ContatoResponsavel(t.ContatoNome, t.ContatoTelefone), docs)),
            cancellationToken);
        if (atribuicao.IsFailure)
        {
            return Result.Failure<TransladoResponse>(atribuicao.Error);
        }

        t.VincularAtribuicao(atribuicao.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return TransladoResponse.De(t);
    }
}

/// <summary>Regra comum de mudança de status, pela equipe ou pela empresa (TRA-03, TRA-05).</summary>
public sealed class MudancaDeStatus(ITransladoUnitOfWork unitOfWork, DocumentosDoTranslado documentos, ICurrentUser usuario, TimeProvider timeProvider)
{
    public async Task<Result> AplicarAsync(TransladoEntity translado, StatusTranslado destino, string? observacao, CancellationToken cancellationToken)
    {
        if (destino == StatusTranslado.EmColeta)
        {
            PendenciasDocumentosResponse pendencias = await documentos.PendenciasAsync(translado, cancellationToken);
            if (!pendencias.Completo)
            {
                return TransladoErros.DocumentosFaltando(pendencias.Documentos.Where(d => !d.Presente).Select(d => d.Tipo));
            }
        }

        Result mudou = translado.MudarPara(destino, timeProvider.GetUtcNow().UtcDateTime, usuario.IdUsuario, observacao);
        if (mudou.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return mudou;
    }
}

public sealed class AlterarStatusTransladoCommandHandler(ITransladoRepository translados, MudancaDeStatus mudanca) : ICommandHandler<AlterarStatusTransladoCommand, TransladoResponse>
{
    public async ValueTask<Result<TransladoResponse>> Handle(AlterarStatusTransladoCommand command, CancellationToken cancellationToken)
    {
        if (await translados.ObterAsync(command.IdTranslado, cancellationToken) is not TransladoEntity t)
        {
            return TransladoErros.NaoEncontrado;
        }

        Result mudou = await mudanca.AplicarAsync(t, command.Status!.Value, command.Observacao, cancellationToken);
        return mudou.IsSuccess ? TransladoResponse.De(t) : Result.Failure<TransladoResponse>(mudou.Error);
    }
}

public sealed class AlterarStatusPelaFrotaCommandHandler(AcessoAoTranslado acesso, MudancaDeStatus mudanca, ICurrentUser usuario)
    : ICommandHandler<AlterarStatusPelaFrotaCommand, TransladoFrotaResponse>
{
    public async ValueTask<Result<TransladoFrotaResponse>> Handle(AlterarStatusPelaFrotaCommand command, CancellationToken cancellationToken)
    {
        if (await acesso.ObterDaFrotaAsync(command.IdTranslado, usuario.IdTerceiro, cancellationToken) is not TransladoEntity t)
        {
            return TransladoErros.NaoEncontrado;
        }

        Result mudou = await mudanca.AplicarAsync(t, command.Status!.Value, command.Observacao, cancellationToken);
        return mudou.IsSuccess ? TransladoFrotaResponse.De(t) : Result.Failure<TransladoFrotaResponse>(mudou.Error);
    }
}

public sealed class InformarMotoristaCommandHandler(AcessoAoTranslado acesso, ITransladoUnitOfWork unitOfWork, ICurrentUser usuario)
    : ICommandHandler<InformarMotoristaCommand, TransladoFrotaResponse>
{
    public async ValueTask<Result<TransladoFrotaResponse>> Handle(InformarMotoristaCommand command, CancellationToken cancellationToken)
    {
        if (await acesso.ObterDaFrotaAsync(command.IdTranslado, usuario.IdTerceiro, cancellationToken) is not TransladoEntity t)
        {
            return TransladoErros.NaoEncontrado;
        }

        Result informado = t.InformarMotorista(command.Nome!, command.Telefone!, command.Placa!, command.Modelo!);
        if (informado.IsFailure)
        {
            return Result.Failure<TransladoFrotaResponse>(informado.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return TransladoFrotaResponse.De(t);
    }
}

public sealed class MeusTransladosQueryHandler(ITransladoRepository translados, ICurrentUser usuario) : IQueryHandler<MeusTransladosQuery, IReadOnlyList<TransladoFrotaResponse>>
{
    public async ValueTask<Result<IReadOnlyList<TransladoFrotaResponse>>> Handle(MeusTransladosQuery query, CancellationToken cancellationToken) =>
        (await translados.ListarDaFrotaAsync(usuario.IdTerceiro!.Value, cancellationToken)).Select(TransladoFrotaResponse.De).ToList();
}

public sealed class ObterTransladoFrotaQueryHandler(AcessoAoTranslado acesso, ICurrentUser usuario) : IQueryHandler<ObterTransladoFrotaQuery, TransladoFrotaResponse>
{
    public async ValueTask<Result<TransladoFrotaResponse>> Handle(ObterTransladoFrotaQuery query, CancellationToken cancellationToken) =>
        await acesso.ObterDaFrotaAsync(query.IdTranslado, usuario.IdTerceiro, cancellationToken) is TransladoEntity t
            ? TransladoFrotaResponse.De(t)
            : TransladoErros.NaoEncontrado;
}

public sealed class BaixarDocumentoFrotaQueryHandler(AcessoAoTranslado acesso, IDocumentosApi documentos, ICurrentUser usuario)
    : IQueryHandler<BaixarDocumentoFrotaQuery, LinkDocumentoDto>
{
    public async ValueTask<Result<LinkDocumentoDto>> Handle(BaixarDocumentoFrotaQuery query, CancellationToken cancellationToken)
    {
        TransladoEntity? t = await acesso.ObterDaFrotaAsync(query.IdTranslado, usuario.IdTerceiro, cancellationToken);
        LinkDocumentoDto? link = t is null ? null : await documentos.GerarLinkAsync(t.IdBeneficiario, query.IdDocumento, cancellationToken);
        return link is null ? TransladoErros.NaoEncontrado : link;
    }
}

/// <summary>A empresa aceitou no portal (TER-02).</summary>
internal sealed class TransladoAceitoPelaFrota(ITransladoRepository translados, ITransladoUnitOfWork unitOfWork) : IIntegrationEventHandler<AtribuicaoAceitaIntegrationEvent>
{
    public async Task Handle(AtribuicaoAceitaIntegrationEvent evento, CancellationToken cancellationToken)
    {
        if (evento.Tipo == "TRANSLADO" && await translados.ObterAsync(evento.IdOrigem, cancellationToken) is TransladoEntity t && t.IdAtribuicao == evento.IdAtribuicao)
        {
            t.RegistrarAceite();
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}

/// <summary>A empresa recusou no portal: o translado volta para nova atribuição. A funerária já é avisada pelas atribuições (TER-02).</summary>
internal sealed class TransladoRecusadoPelaFrota(ITransladoRepository translados, ITransladoUnitOfWork unitOfWork) : IIntegrationEventHandler<AtribuicaoRecusadaIntegrationEvent>
{
    public async Task Handle(AtribuicaoRecusadaIntegrationEvent evento, CancellationToken cancellationToken)
    {
        if (evento.Tipo == "TRANSLADO" && await translados.ObterAsync(evento.IdOrigem, cancellationToken) is TransladoEntity t && t.IdAtribuicao == evento.IdAtribuicao)
        {
            t.RegistrarRecusa(evento.Motivo);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}

/// <summary>Funeral cancelado cancela os translados em aberto.</summary>
internal sealed class CancelarTransladosDoFuneral(ITransladoRepository translados, ITransladoUnitOfWork unitOfWork, TimeProvider timeProvider)
    : IIntegrationEventHandler<FuneralStatusAlteradoIntegrationEvent>
{
    public async Task Handle(FuneralStatusAlteradoIntegrationEvent evento, CancellationToken cancellationToken)
    {
        if (evento.Status != "CANCELADO")
        {
            return;
        }

        foreach (TransladoEntity t in (await translados.ListarDoFuneralAsync(evento.IdFuneral, cancellationToken)).Where(t => !t.Encerrado))
        {
            t.MudarPara(StatusTranslado.Cancelado, timeProvider.GetUtcNow().UtcDateTime, idUsuario: null, "Funeral cancelado.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Status mudou: avisa quem acompanha em tempo real (TRA-06) e encerra a atribuição quando o serviço termina (TER-02).</summary>
internal sealed class TransladoStatusAlteradoDomainEventHandler(
    ITransladoRepository translados,
    IEntregaStatusTranslado entrega,
    ITerceirosAtribuicoes atribuicoes,
    TimeProvider timeProvider)
    : IDomainEventHandler<TransladoStatusAlteradoDomainEvent>
{
    public async Task Handle(TransladoStatusAlteradoDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await entrega.EnviarAsync(domainEvent.IdTranslado, domainEvent.Status, timeProvider.GetUtcNow().UtcDateTime, domainEvent.Observacao, cancellationToken);

        if (domainEvent.Status is StatusTranslado.Entregue or StatusTranslado.Cancelado
            && await translados.ObterAsync(domainEvent.IdTranslado, cancellationToken) is { IdAtribuicao: Guid idAtribuicao })
        {
            await atribuicoes.EncerrarAsync(idAtribuicao, domainEvent.Status == StatusTranslado.Entregue, cancellationToken);
        }
    }
}
