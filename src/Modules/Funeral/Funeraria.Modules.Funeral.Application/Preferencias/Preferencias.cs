using FluentValidation;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Catalogo.Contracts;
using Funeraria.Modules.Clientes.Contracts;
using Funeraria.Modules.Contratos.Contracts;
using Funeraria.Modules.Funeral.Application.Abstractions;
using Funeraria.Modules.Funeral.Domain.Preferencias;
using Funeraria.Modules.Planos.Contracts;

namespace Funeraria.Modules.Funeral.Application.Preferencias;

/// <summary>Preferências do titular logado e dos dependentes dele, com o que o plano de cada um cobre (FUN-02).</summary>
public sealed record ListarPreferenciasQuery : IQuery<IReadOnlyList<PessoaPreferenciaResponse>>;

/// <summary>O titular registra ou altera as preferências dele ou de um dependente (FUN-02).</summary>
public sealed record DefinirPreferenciaCommand(
    Guid IdPessoa,
    Destinacao? Destinacao,
    TipoCerimonia? TipoCerimonia,
    string? CrencaReligiosa,
    IReadOnlyList<string>? Musicas,
    string? Mensagens,
    string? Observacoes,
    IReadOnlyList<ItemPreferidoRequest>? Itens)
    : ICommand<PessoaPreferenciaResponse>;

public sealed record ItemPreferidoRequest(Guid? IdItemCatalogo, Guid? IdVariacao, int? Quantidade);

/// <param name="IdPessoa">Titular (id do cliente) ou dependente (id do dependente).</param>
/// <param name="Tipo">TITULAR ou DEPENDENTE.</param>
/// <param name="Nome">Nome da pessoa.</param>
/// <param name="Preferencia">O que foi registrado; vazio se nada foi registrado ainda.</param>
public sealed record PessoaPreferenciaResponse(Guid IdPessoa, TipoPessoa Tipo, string Nome, PreferenciaResponse? Preferencia);

public sealed record PreferenciaResponse(
    Destinacao Destinacao,
    TipoCerimonia TipoCerimonia,
    string? CrencaReligiosa,
    IReadOnlyList<string> Musicas,
    string? Mensagens,
    string? Observacoes,
    IReadOnlyList<ItemPreferidoResponse> Itens);

/// <param name="IdItemCatalogo">Item do catálogo.</param>
/// <param name="Nome">Nome atual do item.</param>
/// <param name="Categoria">Categoria atual do item.</param>
/// <param name="IdVariacao">Variação escolhida.</param>
/// <param name="Variacao">Nome da variação.</param>
/// <param name="Quantidade">Quantidade desejada.</param>
/// <param name="CobertoPeloPlano">O plano do contrato que cobre a pessoa inclui o item.</param>
/// <param name="QuantidadeCoberta">Quantos o plano cobre; o que passar disso é cobrado à parte (FUN-04).</param>
public sealed record ItemPreferidoResponse(
    Guid IdItemCatalogo,
    string Nome,
    string Categoria,
    Guid? IdVariacao,
    string? Variacao,
    int Quantidade,
    bool CobertoPeloPlano,
    int QuantidadeCoberta);

internal sealed class DefinirPreferenciaCommandValidator : AbstractValidator<DefinirPreferenciaCommand>
{
    public DefinirPreferenciaCommandValidator()
    {
        RuleFor(c => c.Destinacao).IsInEnum().WithMessage("Destinação inválida. Use NAO_DEFINIDA, SEPULTAMENTO ou CREMACAO.");
        RuleFor(c => c.TipoCerimonia).IsInEnum().WithMessage("Tipo de cerimônia inválido.");
        RuleFor(c => c.CrencaReligiosa).MaximumLength(100).WithMessage("Crença religiosa deve ter no máximo 100 caracteres.");
        RuleFor(c => c.Mensagens).MaximumLength(2000).WithMessage("Mensagens devem ter no máximo 2000 caracteres.");
        RuleFor(c => c.Observacoes).MaximumLength(2000).WithMessage("Observações devem ter no máximo 2000 caracteres.");
        RuleFor(c => c.Musicas)
            .Must(m => m is null || (m.Count <= 20 && m.All(x => x.Length <= 200)))
            .WithMessage("Informe até 20 músicas, com até 200 caracteres cada.");
        RuleForEach(c => c.Itens).ChildRules(item =>
        {
            item.RuleFor(i => i.IdItemCatalogo).NotEmpty().WithMessage("Item do catálogo é obrigatório.");
            item.RuleFor(i => i.Quantidade).GreaterThanOrEqualTo(1).WithMessage("Quantidade deve ser pelo menos 1.");
        });
        RuleFor(c => c.Itens)
            .Must(itens => itens is null || itens.GroupBy(i => (i.IdItemCatalogo, i.IdVariacao)).All(g => g.Count() == 1))
            .WithMessage("Item repetido: informe cada item (e variação) uma vez, com a quantidade.");
    }
}

public sealed class ListarPreferenciasQueryHandler(PessoasDoTitular pessoas, IPreferenciaRepository preferencias, MontagemPreferencia montagem, ICurrentUser usuario)
    : IQueryHandler<ListarPreferenciasQuery, IReadOnlyList<PessoaPreferenciaResponse>>
{
    public async ValueTask<Result<IReadOnlyList<PessoaPreferenciaResponse>>> Handle(ListarPreferenciasQuery query, CancellationToken cancellationToken)
    {
        if (usuario.IdCliente is not Guid idCliente)
        {
            return Result.Failure<IReadOnlyList<PessoaPreferenciaResponse>>(PreferenciaErros.PessoaNaoEncontrada);
        }

        Dictionary<Guid, PreferenciaFuneral> registradas = (await preferencias.ListarDoTitularAsync(idCliente, cancellationToken)).ToDictionary(p => p.IdPessoa);
        var resposta = new List<PessoaPreferenciaResponse>();
        foreach (Pessoa pessoa in await pessoas.ListarAsync(idCliente, cancellationToken))
        {
            resposta.Add(await montagem.MontarAsync(idCliente, pessoa, registradas.GetValueOrDefault(pessoa.Id), cancellationToken));
        }

        return resposta;
    }
}

public sealed class DefinirPreferenciaCommandHandler(
    PessoasDoTitular pessoas,
    IPreferenciaRepository preferencias,
    IFuneralUnitOfWork unitOfWork,
    ICatalogoConsultas catalogo,
    MontagemPreferencia montagem,
    ICurrentUser usuario,
    ITenantContext tenantContext)
    : ICommandHandler<DefinirPreferenciaCommand, PessoaPreferenciaResponse>
{
    public async ValueTask<Result<PessoaPreferenciaResponse>> Handle(DefinirPreferenciaCommand command, CancellationToken cancellationToken)
    {
        Pessoa? pessoa = usuario.IdCliente is Guid idCliente
            ? (await pessoas.ListarAsync(idCliente, cancellationToken)).FirstOrDefault(p => p.Id == command.IdPessoa)
            : null;
        if (pessoa is null)
        {
            return PreferenciaErros.PessoaNaoEncontrada;
        }

        ItemPreferido[] itens = [.. (command.Itens ?? []).Select(i => new ItemPreferido(i.IdItemCatalogo!.Value, i.IdVariacao, i.Quantidade ?? 1))];
        Guid[] invalidos = await ItensInvalidosAsync(itens, cancellationToken);
        if (invalidos.Length > 0)
        {
            return PreferenciaErros.ItensInvalidos(invalidos);
        }

        var dados = new DadosPreferencia(
            command.Destinacao ?? Destinacao.NaoDefinida,
            command.TipoCerimonia ?? TipoCerimonia.NaoDefinida,
            command.CrencaReligiosa,
            command.Musicas ?? [],
            command.Mensagens,
            command.Observacoes,
            itens);

        Guid titular = usuario.IdCliente!.Value;
        PreferenciaFuneral? existente = await preferencias.ObterDaPessoaAsync(titular, pessoa.Id, cancellationToken);
        if (existente is null)
        {
            existente = PreferenciaFuneral.Criar(tenantContext.RequiredTenantId, titular, pessoa.Id, pessoa.Tipo, dados);
            preferencias.Adicionar(existente);
        }
        else
        {
            existente.Atualizar(dados);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await montagem.MontarAsync(titular, pessoa, existente, cancellationToken);
    }

    /// <summary>Item e variação precisam existir e estar ativos no catálogo da funerária.</summary>
    private async Task<Guid[]> ItensInvalidosAsync(ItemPreferido[] itens, CancellationToken cancellationToken)
    {
        if (itens.Length == 0)
        {
            return [];
        }

        Dictionary<Guid, ItemCatalogoDto> doCatalogo = (await catalogo.ObterItensAsync([.. itens.Select(i => i.IdItemCatalogo).Distinct()], cancellationToken))
            .ToDictionary(i => i.IdItem);

        return [.. itens
            .Where(i => !doCatalogo.TryGetValue(i.IdItemCatalogo, out ItemCatalogoDto? item)
                || !item.Ativo
                || (i.IdVariacao is Guid v && item.Variacoes.All(x => x.IdVariacao != v)))
            .Select(i => i.IdVariacao ?? i.IdItemCatalogo)];
    }
}

/// <summary>Pessoa que pode ter preferência registrada pelo titular: ele mesmo e os dependentes ativos.</summary>
public sealed record Pessoa(Guid Id, TipoPessoa Tipo, string Nome);

public sealed class PessoasDoTitular(IClientesApi clientes)
{
    public async Task<IReadOnlyList<Pessoa>> ListarAsync(Guid idCliente, CancellationToken cancellationToken)
    {
        TitularDto? titular = await clientes.ObterTitularAsync(idCliente, cancellationToken);
        if (titular is null)
        {
            return [];
        }

        IReadOnlyList<DependenteDto> dependentes = await clientes.ListarDependentesDoTitularAsync(idCliente, cancellationToken);
        return [new Pessoa(titular.IdCliente, TipoPessoa.Titular, titular.Nome), .. dependentes.Select(d => new Pessoa(d.IdDependente, TipoPessoa.Dependente, d.Nome))];
    }
}

/// <summary>Monta a resposta com os nomes atuais do catálogo e o que o plano da pessoa cobre.</summary>
public sealed class MontagemPreferencia(ICatalogoConsultas catalogo, IContratosApi contratos, IPlanosApi planos)
{
    public async Task<PessoaPreferenciaResponse> MontarAsync(Guid idCliente, Pessoa pessoa, PreferenciaFuneral? preferencia, CancellationToken cancellationToken)
    {
        if (preferencia is null)
        {
            return new PessoaPreferenciaResponse(pessoa.Id, pessoa.Tipo, pessoa.Nome, null);
        }

        Guid? plano = await contratos.PlanoQueCobreAsync(idCliente, pessoa.Tipo == TipoPessoa.Dependente ? pessoa.Id : null, cancellationToken);
        Dictionary<Guid, int> cobertos = plano is Guid id
            ? (await planos.ObterCoberturasAsync(id, cancellationToken)).ToDictionary(c => c.IdItemCatalogo, c => c.Quantidade)
            : [];
        Dictionary<Guid, ItemCatalogoDto> itens = (await catalogo.ObterItensAsync([.. preferencia.Itens.Select(i => i.IdItemCatalogo)], cancellationToken))
            .ToDictionary(i => i.IdItem);

        ItemPreferidoResponse Item(ItemPreferido i)
        {
            ItemCatalogoDto? doCatalogo = itens.GetValueOrDefault(i.IdItemCatalogo);
            int coberta = cobertos.GetValueOrDefault(i.IdItemCatalogo);
            return new ItemPreferidoResponse(
                i.IdItemCatalogo,
                doCatalogo?.Nome ?? string.Empty,
                doCatalogo?.Categoria ?? string.Empty,
                i.IdVariacao,
                doCatalogo?.Variacoes.FirstOrDefault(v => v.IdVariacao == i.IdVariacao)?.Nome,
                i.Quantidade,
                coberta > 0,
                Math.Min(coberta, i.Quantidade));
        }

        return new PessoaPreferenciaResponse(pessoa.Id, pessoa.Tipo, pessoa.Nome, new PreferenciaResponse(
            preferencia.Destinacao,
            preferencia.TipoCerimonia,
            preferencia.CrencaReligiosa,
            preferencia.Musicas,
            preferencia.Mensagens,
            preferencia.Observacoes,
            [.. preferencia.Itens.Select(Item)]));
    }
}
