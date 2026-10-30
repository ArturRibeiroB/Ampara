using FluentValidation;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Catalogo.Contracts;
using Funeraria.Modules.Contratos.Contracts;
using Funeraria.Modules.Funeral.Application.Abstractions;
using Funeraria.Modules.Funeral.Application.Funerais;
using Funeraria.Modules.Funeral.Domain.Beneficiarios;
using Funeraria.Modules.Funeral.Domain.Funerais;
using Funeraria.Modules.Funeral.Domain.Preferencias;
using Funeraria.Modules.Planos.Contracts;
using FuneralEntity = Funeraria.Modules.Funeral.Domain.Funerais.Funeral;

namespace Funeraria.Modules.Funeral.Application.Itens;

/// <summary>Itens do funeral separados em cobertos pelo plano e adicionais (FUN-03).</summary>
public sealed record ItensFuneralQuery(Guid IdFuneral) : IQuery<ComposicaoItensResponse>;

/// <summary>Inclui um item do catálogo no funeral. Item com estoque baixa a quantidade na hora (FUN-03).</summary>
public sealed record AdicionarItemFuneralCommand(Guid IdFuneral, Guid? IdItemCatalogo, Guid? IdVariacao, int? Quantidade)
    : ICommand<ComposicaoItensResponse>;

public sealed record RemoverItemFuneralCommand(Guid IdFuneral, Guid IdFuneralItem) : ICommand<ComposicaoItensResponse>;

/// <summary>Traz para o funeral os itens das preferências registradas em vida pelo falecido (FUN-02, FUN-03).</summary>
public sealed record ImportarPreferenciasCommand(Guid IdFuneral) : ICommand<ImportacaoPreferenciasResponse>;

/// <param name="ItensCobertos">O que o plano paga (até a quantidade da cobertura do PLA-02).</param>
/// <param name="ItensAdicionais">O que a família paga: o que passa da cobertura e a diferença das variações mais caras.</param>
/// <param name="TotalAdicional">Soma dos adicionais, base do orçamento (FUN-04).</param>
public sealed record ComposicaoItensResponse(
    IReadOnlyList<ItemCobertoResponse> ItensCobertos,
    IReadOnlyList<ItemAdicionalResponse> ItensAdicionais,
    decimal TotalAdicional);

public sealed record ItemCobertoResponse(
    Guid IdFuneralItem, Guid IdItemCatalogo, string Nome, string Categoria, string? Variacao, int Quantidade);

/// <param name="IdFuneralItem">Item do funeral.</param>
/// <param name="IdItemCatalogo">Item do catálogo.</param>
/// <param name="Nome">Nome do item na escolha.</param>
/// <param name="Categoria">Categoria do item.</param>
/// <param name="Variacao">Variação escolhida.</param>
/// <param name="Quantidade">Quantidade cobrada.</param>
/// <param name="PrecoUnitario">Preço cobrado por unidade.</param>
/// <param name="Subtotal">Quantidade × preço unitário.</param>
/// <param name="SomenteDiferencaVariacao">Item coberto pelo plano: a família paga só a diferença da variação escolhida.</param>
public sealed record ItemAdicionalResponse(
    Guid IdFuneralItem,
    Guid IdItemCatalogo,
    string Nome,
    string Categoria,
    string? Variacao,
    int Quantidade,
    decimal PrecoUnitario,
    decimal Subtotal,
    bool SomenteDiferencaVariacao);

/// <param name="Importados">Quantos itens das preferências entraram.</param>
/// <param name="Ignorados">Itens que não entraram e por quê (já no funeral, fora do catálogo, sem estoque).</param>
/// <param name="Composicao">Itens do funeral depois da importação.</param>
public sealed record ImportacaoPreferenciasResponse(int Importados, IReadOnlyList<ItemIgnoradoResponse> Ignorados, ComposicaoItensResponse Composicao);

public sealed record ItemIgnoradoResponse(Guid IdItemCatalogo, string Motivo);

internal sealed class AdicionarItemFuneralCommandValidator : AbstractValidator<AdicionarItemFuneralCommand>
{
    public AdicionarItemFuneralCommandValidator()
    {
        RuleFor(c => c.IdItemCatalogo).NotEmpty().WithMessage("Item do catálogo é obrigatório.");
        RuleFor(c => c.Quantidade).NotNull().WithMessage("Quantidade é obrigatória.")
            .InclusiveBetween(1, 1000).WithMessage("Quantidade deve ser de 1 a 1000.");
    }
}

internal static class ItensErros
{
    public static readonly Error ItemIndisponivel =
        Error.Validacao("Funeral.ItemIndisponivel", "Item do catálogo não encontrado ou inativo.");

    public static readonly Error VariacaoInvalida =
        Error.Validacao("Funeral.VariacaoInvalida", "Variação não encontrada ou inativa para este item.");

    public static readonly Error EstoqueInsuficiente =
        Error.Conflito("Funeral.EstoqueInsuficiente", "Estoque insuficiente para este item.");

    public static readonly Error SemPreferencias =
        Error.NaoEncontrado("Funeral.SemPreferencias", "O falecido não registrou preferências de funeral.");
}

/// <summary>Monta o item a partir do catálogo e baixa o estoque, se o item controlar (FUN-03).</summary>
public sealed class EscolhaNoCatalogo(ICatalogoEstoque estoque)
{
    public async Task<Result<ItemEscolhido>> EscolherAsync(
        ItemCatalogoDto? item, Guid? idVariacao, int quantidade, OrigemItem origem, CancellationToken cancellationToken)
    {
        if (item is not { Ativo: true })
        {
            return ItensErros.ItemIndisponivel;
        }

        VariacaoDto? variacao = null;
        if (idVariacao is Guid id)
        {
            variacao = item.Variacoes.FirstOrDefault(v => v.IdVariacao == id);
            if (variacao is null)
            {
                return ItensErros.VariacaoInvalida;
            }
        }

        if (item.ControlaEstoque && !await estoque.BaixarAsync(item.IdItem, quantidade, cancellationToken))
        {
            return ItensErros.EstoqueInsuficiente;
        }

        return new ItemEscolhido(
            item.IdItem, variacao?.IdVariacao, item.Nome, item.Categoria, variacao?.Nome, quantidade, item.Preco,
            variacao?.AcrescimoPreco ?? 0, item.IdTerceiroFornecedor, item.ControlaEstoque, origem);
    }

    /// <summary>Devolve o estoque de um item que não chegou a ser gravado ou saiu do funeral.</summary>
    public Task DevolverAsync(Guid idItemCatalogo, bool estoqueBaixado, int quantidade, CancellationToken cancellationToken) =>
        estoqueBaixado ? estoque.DevolverAsync(idItemCatalogo, quantidade, cancellationToken) : Task.CompletedTask;
}

/// <summary>Separa o coberto do adicional com as quantidades da cobertura do plano do contrato (PLA-02).</summary>
public sealed class ComposicaoDosItens(IContratosApi contratos, IPlanosApi planos)
{
    public async Task<ComposicaoItensResponse> MontarAsync(FuneralEntity funeral, CancellationToken cancellationToken)
    {
        Guid? plano = await contratos.PlanoDoContratoAsync(funeral.IdContrato, cancellationToken);
        Dictionary<Guid, int> restante = plano is Guid id
            ? (await planos.ObterCoberturasAsync(id, cancellationToken)).ToDictionary(c => c.IdItemCatalogo, c => c.Quantidade)
            : [];

        var cobertos = new List<ItemCobertoResponse>();
        var adicionais = new List<ItemAdicionalResponse>();

        // Na ordem de escolha: a cobertura vai para os primeiros itens; o que passar dela é adicional.
        foreach (FuneralItem item in funeral.Itens.OrderBy(i => i.IncluidoEm).ThenBy(i => i.Id))
        {
            int coberta = Math.Min(item.Quantidade, restante.GetValueOrDefault(item.IdItemCatalogo));
            restante[item.IdItemCatalogo] = restante.GetValueOrDefault(item.IdItemCatalogo) - coberta;
            int adicional = item.Quantidade - coberta;

            if (coberta > 0)
            {
                cobertos.Add(new ItemCobertoResponse(item.Id, item.IdItemCatalogo, item.Nome, item.Categoria, item.NomeVariacao, coberta));
                if (item.AcrescimoVariacao > 0)
                {
                    adicionais.Add(Adicional(item, coberta, item.AcrescimoVariacao, somenteDiferenca: true));
                }
            }

            if (adicional > 0)
            {
                adicionais.Add(Adicional(item, adicional, item.PrecoUnitario, somenteDiferenca: false));
            }
        }

        return new ComposicaoItensResponse(cobertos, adicionais, adicionais.Sum(a => a.Subtotal));
    }

    private static ItemAdicionalResponse Adicional(FuneralItem item, int quantidade, decimal preco, bool somenteDiferenca) =>
        new(item.Id, item.IdItemCatalogo, item.Nome, item.Categoria, item.NomeVariacao, quantidade, preco, preco * quantidade, somenteDiferenca);
}

public sealed class ItensFuneralQueryHandler(IFuneralRepository funerais, ComposicaoDosItens composicao, ICurrentUser usuario)
    : IQueryHandler<ItensFuneralQuery, ComposicaoItensResponse>
{
    public async ValueTask<Result<ComposicaoItensResponse>> Handle(ItensFuneralQuery query, CancellationToken cancellationToken)
    {
        FuneralEntity? funeral = await funerais.ObterAsync(query.IdFuneral, cancellationToken);
        return funeral is not null && AcessoDaFamilia.Permitido(usuario, funeral.IdCliente)
            ? await composicao.MontarAsync(funeral, cancellationToken)
            : FuneralErros.NaoEncontrado;
    }
}

public sealed class AdicionarItemFuneralCommandHandler(
    IFuneralRepository funerais,
    IFuneralUnitOfWork unitOfWork,
    ICatalogoConsultas catalogo,
    EscolhaNoCatalogo escolha,
    ComposicaoDosItens composicao,
    TimeProvider timeProvider)
    : ICommandHandler<AdicionarItemFuneralCommand, ComposicaoItensResponse>
{
    public async ValueTask<Result<ComposicaoItensResponse>> Handle(AdicionarItemFuneralCommand command, CancellationToken cancellationToken)
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

        ItemCatalogoDto? item = (await catalogo.ObterItensAsync([command.IdItemCatalogo!.Value], cancellationToken)).FirstOrDefault();
        Result<ItemEscolhido> escolhido = await escolha.EscolherAsync(item, command.IdVariacao, command.Quantidade!.Value, OrigemItem.Manual, cancellationToken);
        if (escolhido.IsFailure)
        {
            return Result.Failure<ComposicaoItensResponse>(escolhido.Error);
        }

        funeral.AdicionarItem(escolhido.Value, timeProvider.GetUtcNow().UtcDateTime);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // O estoque é de outro módulo (outra transação): se o funeral não gravou, a baixa é desfeita.
            await escolha.DevolverAsync(escolhido.Value.IdItemCatalogo, escolhido.Value.EstoqueBaixado, escolhido.Value.Quantidade, CancellationToken.None);
            throw;
        }

        return await composicao.MontarAsync(funeral, cancellationToken);
    }
}

public sealed class RemoverItemFuneralCommandHandler(
    IFuneralRepository funerais,
    IFuneralUnitOfWork unitOfWork,
    EscolhaNoCatalogo escolha,
    ComposicaoDosItens composicao)
    : ICommandHandler<RemoverItemFuneralCommand, ComposicaoItensResponse>
{
    public async ValueTask<Result<ComposicaoItensResponse>> Handle(RemoverItemFuneralCommand command, CancellationToken cancellationToken)
    {
        FuneralEntity? funeral = await funerais.ObterAsync(command.IdFuneral, cancellationToken);
        if (funeral is null)
        {
            return FuneralErros.NaoEncontrado;
        }

        Result<FuneralItem> removido = funeral.RemoverItem(command.IdFuneralItem);
        if (removido.IsFailure)
        {
            return Result.Failure<ComposicaoItensResponse>(removido.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await escolha.DevolverAsync(removido.Value.IdItemCatalogo, removido.Value.EstoqueBaixado, removido.Value.Quantidade, cancellationToken);
        return await composicao.MontarAsync(funeral, cancellationToken);
    }
}

public sealed class ImportarPreferenciasCommandHandler(
    IFuneralRepository funerais,
    IBeneficiarioRepository beneficiarios,
    IPreferenciaRepository preferencias,
    IFuneralUnitOfWork unitOfWork,
    ICatalogoConsultas catalogo,
    EscolhaNoCatalogo escolha,
    ComposicaoDosItens composicao,
    TimeProvider timeProvider)
    : ICommandHandler<ImportarPreferenciasCommand, ImportacaoPreferenciasResponse>
{
    public async ValueTask<Result<ImportacaoPreferenciasResponse>> Handle(ImportarPreferenciasCommand command, CancellationToken cancellationToken)
    {
        FuneralEntity? funeral = await funerais.ObterAsync(command.IdFuneral, cancellationToken);
        Beneficiario? beneficiario = funeral is null ? null : await beneficiarios.ObterAsync(funeral.IdBeneficiario, cancellationToken);
        if (funeral is null || beneficiario is null)
        {
            return FuneralErros.NaoEncontrado;
        }

        if (funeral.Encerrado)
        {
            return FuneralErros.Encerrado;
        }

        // A preferência é por pessoa: o titular é o próprio cliente; o dependente, o id do dependente (FUN-02).
        PreferenciaFuneral? preferencia = await preferencias.ObterDaPessoaAsync(
            beneficiario.IdCliente, beneficiario.IdDependente ?? beneficiario.IdCliente, cancellationToken);
        if (preferencia is null)
        {
            return ItensErros.SemPreferencias;
        }

        Dictionary<Guid, ItemCatalogoDto> doCatalogo = (await catalogo.ObterItensAsync([.. preferencia.Itens.Select(i => i.IdItemCatalogo)], cancellationToken))
            .ToDictionary(i => i.IdItem);
        var ignorados = new List<ItemIgnoradoResponse>();
        var escolhidos = new List<ItemEscolhido>();
        DateTime agora = timeProvider.GetUtcNow().UtcDateTime;

        foreach (ItemPreferido preferido in preferencia.Itens)
        {
            if (funeral.Itens.Any(i => i.IdItemCatalogo == preferido.IdItemCatalogo && i.IdVariacao == preferido.IdVariacao))
            {
                ignorados.Add(new ItemIgnoradoResponse(preferido.IdItemCatalogo, "Já está no funeral."));
                continue;
            }

            Result<ItemEscolhido> escolhido = await escolha.EscolherAsync(
                doCatalogo.GetValueOrDefault(preferido.IdItemCatalogo), preferido.IdVariacao, preferido.Quantidade, OrigemItem.Preferencia, cancellationToken);
            if (escolhido.IsFailure)
            {
                ignorados.Add(new ItemIgnoradoResponse(preferido.IdItemCatalogo, escolhido.Error.Description));
                continue;
            }

            escolhidos.Add(escolhido.Value);
            funeral.AdicionarItem(escolhido.Value, agora);
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            foreach (ItemEscolhido e in escolhidos)
            {
                await escolha.DevolverAsync(e.IdItemCatalogo, e.EstoqueBaixado, e.Quantidade, CancellationToken.None);
            }

            throw;
        }

        return new ImportacaoPreferenciasResponse(escolhidos.Count, ignorados, await composicao.MontarAsync(funeral, cancellationToken));
    }
}

/// <summary>Funeral cancelado devolve ao catálogo o estoque baixado pelos itens (FUN-03).</summary>
internal sealed class DevolverEstoqueDoFuneralCancelado(IFuneralRepository funerais, IFuneralUnitOfWork unitOfWork, EscolhaNoCatalogo escolha)
    : IDomainEventHandler<FuneralStatusAlteradoDomainEvent>
{
    public async Task Handle(FuneralStatusAlteradoDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (domainEvent.Atual != StatusFuneral.Cancelado || await funerais.ObterAsync(domainEvent.IdFuneral, cancellationToken) is not FuneralEntity funeral)
        {
            return;
        }

        IReadOnlyList<FuneralItem> devolver = funeral.LiberarEstoque();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        foreach (FuneralItem item in devolver)
        {
            await escolha.DevolverAsync(item.IdItemCatalogo, estoqueBaixado: true, item.Quantidade, cancellationToken);
        }
    }
}
