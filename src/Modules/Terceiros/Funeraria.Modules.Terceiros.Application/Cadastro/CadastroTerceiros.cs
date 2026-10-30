using FluentValidation;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Application.Paginacao;
using Funeraria.BuildingBlocks.Application.Validation;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Catalogo.Contracts;
using Funeraria.Modules.Identidade.Contracts;
using Funeraria.Modules.Terceiros.Application.Abstractions;
using Funeraria.Modules.Terceiros.Domain.Terceiros;

namespace Funeraria.Modules.Terceiros.Application.Cadastro;

public interface IDadosTerceiro
{
    string NomeRazaoSocial { get; }

    string CpfCnpj { get; }

    string? Telefone { get; }

    AreaAtuacao? AreaAtuacao { get; }

    IReadOnlyList<Guid>? CategoriasAtendidas { get; }
}

/// <summary>
/// A funerária cadastra um parceiro (TER-01). Ele recebe o convite para definir a senha; se já tiver conta na plataforma,
/// só ganha o acesso a esta funerária. O convite sai pela Identidade, pelo evento de terceiro cadastrado.
/// </summary>
public sealed record CadastrarTerceiroCommand(
    TipoTerceiro? Tipo,
    string Email,
    string NomeRazaoSocial,
    string CpfCnpj,
    string? Telefone,
    AreaAtuacao? AreaAtuacao,
    IReadOnlyList<Guid>? CategoriasAtendidas)
    : ICommand<TerceiroResponse>, IDadosTerceiro;

/// <summary>Altera os dados do parceiro. Tipo e e-mail não mudam: são o vínculo e a conta de acesso.</summary>
public sealed record AtualizarTerceiroCommand(
    Guid IdTerceiro,
    string NomeRazaoSocial,
    string CpfCnpj,
    string? Telefone,
    AreaAtuacao? AreaAtuacao,
    IReadOnlyList<Guid>? CategoriasAtendidas)
    : ICommand<TerceiroResponse>, IDadosTerceiro;

/// <summary>Desativar não apaga o histórico, tira o acesso a esta funerária e impede novas atribuições (TER-01).</summary>
public sealed record AlterarStatusTerceiroCommand(Guid IdTerceiro, bool? Ativo) : ICommand;

/// <summary>Terceiros da funerária, por tipo (FROTA, CARTORIO_DESPACHANTE, FORNECEDOR) e status (CONVIDADO, ATIVO, INATIVO).</summary>
public sealed record ListarTerceirosQuery(PaginaRequest Pagina, string? Tipo, string? Status)
    : IQuery<PaginaResult<TerceiroResponse>>;

internal static class FiltroEnum
{
    /// <summary>Aceita o formato da API (CARTORIO_DESPACHANTE) e o nome do enum.</summary>
    public static T? Ler<T>(string? valor)
        where T : struct, Enum =>
        valor is not null
        && Enum.TryParse(valor.Replace("_", string.Empty, StringComparison.Ordinal), ignoreCase: true, out T lido)
        && Enum.IsDefined(lido)
            ? lido
            : null;
}

internal sealed class ListarTerceirosQueryValidator : AbstractValidator<ListarTerceirosQuery>
{
    public ListarTerceirosQueryValidator()
    {
        RuleFor(q => q.Tipo).Must(t => t is null || FiltroEnum.Ler<TipoTerceiro>(t) is not null)
            .WithMessage("Tipo inválido. Use FROTA, CARTORIO_DESPACHANTE ou FORNECEDOR.");
        RuleFor(q => q.Status).Must(s => s is null || FiltroEnum.Ler<StatusTerceiro>(s) is not null)
            .WithMessage("Status inválido. Use CONVIDADO, ATIVO ou INATIVO.");
    }
}

public sealed record ObterTerceiroQuery(Guid IdTerceiro) : IQuery<TerceiroResponse>;

public sealed record TerceiroResponse(
    Guid IdTerceiro,
    TipoTerceiro Tipo,
    string NomeRazaoSocial,
    string CpfCnpj,
    string? Telefone,
    string Email,
    AreaAtuacao AreaAtuacao,
    IReadOnlyList<Guid> CategoriasAtendidas,
    StatusTerceiro Status)
{
    public static TerceiroResponse De(Terceiro t) =>
        new(t.Id, t.Tipo, t.NomeRazaoSocial, t.CpfCnpj, t.Telefone, t.Email, t.AreaAtuacao, t.CategoriasAtendidas, t.Status);
}

internal abstract class DadosTerceiroValidator<T> : AbstractValidator<T>
    where T : IDadosTerceiro
{
    protected DadosTerceiroValidator()
    {
        RuleFor(t => t.NomeRazaoSocial)
            .NotEmpty().WithMessage("Nome ou razão social é obrigatório.")
            .MaximumLength(150).WithMessage("Nome ou razão social deve ter no máximo 150 caracteres.");
        RuleFor(t => t.CpfCnpj)
            .Must(d => DocumentosBrasileiros.CpfValido(d) || DocumentosBrasileiros.CnpjValido(d))
            .WithMessage("CPF ou CNPJ não informado ou em formato inválido.");
        RuleFor(t => t.Telefone).Telefone();
        RuleFor(t => t.AreaAtuacao!.Ufs)
            .Must(ufs => ufs.All(RegrasComuns.UfValida)).WithMessage("UF inválida na área de atuação.")
            .When(t => t.AreaAtuacao is not null);
        RuleForEach(t => t.AreaAtuacao!.Cidades)
            .Must(c => !string.IsNullOrWhiteSpace(c.Nome) && c.Uf is not null && RegrasComuns.UfValida(c.Uf))
            .WithMessage("Cidade da área de atuação precisa de nome e UF válida.")
            .When(t => t.AreaAtuacao is not null);
    }
}

internal sealed class CadastrarTerceiroCommandValidator : DadosTerceiroValidator<CadastrarTerceiroCommand>
{
    public CadastrarTerceiroCommandValidator()
    {
        RuleFor(t => t.Tipo).NotNull().WithMessage("Tipo é obrigatório. Use FROTA, CARTORIO_DESPACHANTE ou FORNECEDOR.");
        RuleFor(t => t.Email).EmailObrigatorio();
        RuleFor(t => t.CategoriasAtendidas)
            .Must(c => c is null || c.Count == 0).WithMessage("Categorias atendidas só valem para FORNECEDOR.")
            .When(t => t.Tipo is not null and not TipoTerceiro.Fornecedor);
    }
}

internal sealed class AtualizarTerceiroCommandValidator : DadosTerceiroValidator<AtualizarTerceiroCommand>;

internal sealed class AlterarStatusTerceiroCommandValidator : AbstractValidator<AlterarStatusTerceiroCommand>
{
    public AlterarStatusTerceiroCommandValidator() =>
        RuleFor(c => c.Ativo).NotNull().WithMessage("Informe se o terceiro fica ativo (true) ou não (false).");
}

public sealed class CadastrarTerceiroCommandHandler(
    ITerceiroRepository terceiros,
    ITerceirosUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    IIdentidadeApi identidade,
    ICatalogoConsultas catalogo,
    IAuditoria auditoria)
    : ICommandHandler<CadastrarTerceiroCommand, TerceiroResponse>
{
    public async ValueTask<Result<TerceiroResponse>> Handle(CadastrarTerceiroCommand command, CancellationToken cancellationToken)
    {
        Guid idTenant = tenantContext.RequiredTenantId;
        string email = command.Email.Trim();

        if (await terceiros.ExisteEmailAsync(email, cancellationToken))
        {
            return TerceiroErros.EmailDuplicado;
        }

        // A conta é única por funerária: quem já é cliente ou equipe aqui não vira terceiro com o mesmo e-mail.
        if (await identidade.PerfilNaFunerariaAsync(email, idTenant, cancellationToken) is not null)
        {
            return TerceiroErros.EmailComAcesso;
        }

        Result<DadosTerceiro> dados = await RegrasTerceiro.DadosAsync(command, terceiros, catalogo, exceto: null, cancellationToken);
        if (dados.IsFailure)
        {
            return Result.Failure<TerceiroResponse>(dados.Error);
        }

        var terceiro = Terceiro.Cadastrar(idTenant, command.Tipo!.Value, email, dados.Value);
        terceiros.Adicionar(terceiro);
        Result<TerceiroResponse> salvo = await RegrasTerceiro.SalvarAsync(unitOfWork, terceiro, cancellationToken);
        if (salvo.IsSuccess)
        {
            await auditoria.RegistrarAsync(new RegistroAuditoria(
                AcoesAuditadas.TerceiroCadastrado, "Terceiro", terceiro.Id,
                ValoresNovos: new { tipo = terceiro.Tipo.ToString(), terceiro.NomeRazaoSocial, terceiro.Email }), cancellationToken);
        }

        return salvo;
    }
}

public sealed class AtualizarTerceiroCommandHandler(
    ITerceiroRepository terceiros,
    ITerceirosUnitOfWork unitOfWork,
    ICatalogoConsultas catalogo)
    : ICommandHandler<AtualizarTerceiroCommand, TerceiroResponse>
{
    public async ValueTask<Result<TerceiroResponse>> Handle(AtualizarTerceiroCommand command, CancellationToken cancellationToken)
    {
        Terceiro? terceiro = await terceiros.ObterAsync(command.IdTerceiro, cancellationToken);
        if (terceiro is null)
        {
            return TerceiroErros.NaoEncontrado;
        }

        if (terceiro.Tipo != TipoTerceiro.Fornecedor && command.CategoriasAtendidas is { Count: > 0 })
        {
            return RegrasTerceiro.ErroCategorias("Categorias atendidas só valem para FORNECEDOR.");
        }

        Result<DadosTerceiro> dados = await RegrasTerceiro.DadosAsync(command, terceiros, catalogo, exceto: terceiro.Id, cancellationToken);
        if (dados.IsFailure)
        {
            return Result.Failure<TerceiroResponse>(dados.Error);
        }

        terceiro.Atualizar(dados.Value);
        return await RegrasTerceiro.SalvarAsync(unitOfWork, terceiro, cancellationToken);
    }
}

public sealed class AlterarStatusTerceiroCommandHandler(ITerceiroRepository terceiros, ITerceirosUnitOfWork unitOfWork, IAuditoria auditoria)
    : ICommandHandler<AlterarStatusTerceiroCommand>
{
    public async ValueTask<Result> Handle(AlterarStatusTerceiroCommand command, CancellationToken cancellationToken)
    {
        Terceiro? terceiro = await terceiros.ObterAsync(command.IdTerceiro, cancellationToken);
        if (terceiro is null)
        {
            return TerceiroErros.NaoEncontrado;
        }

        StatusTerceiro anterior = terceiro.Status;
        terceiro.AlterarStatus(command.Ativo!.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await auditoria.RegistrarAsync(new RegistroAuditoria(
            AcoesAuditadas.TerceiroStatusAlterado, "Terceiro", terceiro.Id,
            new { status = anterior.ToString() }, new { status = terceiro.Status.ToString() }), cancellationToken);
        return Result.Success();
    }
}

public sealed class ListarTerceirosQueryHandler(ITerceirosLeitura leitura)
    : IQueryHandler<ListarTerceirosQuery, PaginaResult<TerceiroResponse>>
{
    public async ValueTask<Result<PaginaResult<TerceiroResponse>>> Handle(ListarTerceirosQuery query, CancellationToken cancellationToken)
    {
        PaginaRequest pagina = query.Pagina.Normalizada();
        (IReadOnlyList<Terceiro> itens, long total) = await leitura.ListarAsync(
            FiltroEnum.Ler<TipoTerceiro>(query.Tipo), FiltroEnum.Ler<StatusTerceiro>(query.Status), pagina, cancellationToken);
        return new PaginaResult<TerceiroResponse>([.. itens.Select(TerceiroResponse.De)], pagina.Pagina, pagina.Tamanho, total);
    }
}

public sealed class ObterTerceiroQueryHandler(ITerceiroRepository terceiros) : IQueryHandler<ObterTerceiroQuery, TerceiroResponse>
{
    public async ValueTask<Result<TerceiroResponse>> Handle(ObterTerceiroQuery query, CancellationToken cancellationToken) =>
        await terceiros.ObterAsync(query.IdTerceiro, cancellationToken) is Terceiro t
            ? TerceiroResponse.De(t)
            : TerceiroErros.NaoEncontrado;
}

internal static class RegrasTerceiro
{
    public static async Task<Result<DadosTerceiro>> DadosAsync(
        IDadosTerceiro dados,
        ITerceiroRepository terceiros,
        ICatalogoConsultas catalogo,
        Guid? exceto,
        CancellationToken cancellationToken)
    {
        string cpfCnpj = DocumentosBrasileiros.Normalizar(dados.CpfCnpj);
        if (await terceiros.ExisteCpfCnpjAsync(cpfCnpj, exceto, cancellationToken))
        {
            return TerceiroErros.CpfCnpjDuplicado;
        }

        Guid[] categorias = [.. (dados.CategoriasAtendidas ?? []).Distinct()];
        if (categorias.Length > 0)
        {
            HashSet<Guid> existentes = [.. (await catalogo.ObterCategoriasAsync(categorias, cancellationToken)).Select(c => c.IdCategoria)];
            if (categorias.Any(c => !existentes.Contains(c)))
            {
                return ErroCategorias("Categoria do catálogo não encontrada.");
            }
        }

        AreaAtuacao area = dados.AreaAtuacao is null
            ? new AreaAtuacao()
            : new AreaAtuacao
            {
                Ufs = [.. dados.AreaAtuacao.Ufs.Select(u => u.Trim().ToUpperInvariant()).Distinct()],
                Cidades = [.. dados.AreaAtuacao.Cidades.Select(c => new CidadeAtendida(c.Nome.Trim(), c.Uf.Trim().ToUpperInvariant())).Distinct()]
            };

        return new DadosTerceiro(dados.NomeRazaoSocial, cpfCnpj, dados.Telefone, area, categorias);
    }

    public static Error ErroCategorias(string mensagem) =>
        new ValidationError(new Dictionary<string, string[]> { ["categorias_atendidas"] = [mensagem] });

    public static async Task<Result<TerceiroResponse>> SalvarAsync(ITerceirosUnitOfWork unitOfWork, Terceiro terceiro, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ViolacaoDeUnicidadeException e)
        {
            return e.Restricao?.Contains("email", StringComparison.Ordinal) == true ? TerceiroErros.EmailDuplicado : TerceiroErros.CpfCnpjDuplicado;
        }

        return TerceiroResponse.De(terceiro);
    }
}
