using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Clientes.Contracts;
using Funeraria.Modules.Contratos.Domain.Contratos;

namespace Funeraria.Modules.Contratos.Application.Queries;

/// <summary>Contratos do cliente logado (CON-01).</summary>
public sealed record ListarMeusContratosQuery : IQuery<IReadOnlyList<ContratoResumoResponse>>;

/// <summary>
/// Detalhe do contrato com o plano e as pessoas cobertas (CON-01). A equipe vê qualquer contrato da funerária;
/// o cliente, só os próprios. A situação financeira entra com as faturas (COB-01).
/// </summary>
public sealed record ObterContratoQuery(Guid IdContrato) : IQuery<ContratoDetalheResponse>;

public sealed record ContratoResumoResponse(
    Guid IdContrato,
    Guid IdCliente,
    string NomeTitular,
    Guid IdPlano,
    string NomePlano,
    TipoCobertura TipoCobertura,
    Periodicidade Periodicidade,
    StatusContrato Status,
    decimal ValorTotal,
    DateOnly DataContratacao,
    int QuantidadePessoas);

public sealed record ContratoDetalheResponse(
    Guid IdContrato,
    TitularResponse Titular,
    Guid IdPlano,
    string NomePlano,
    TipoCobertura TipoCobertura,
    Periodicidade Periodicidade,
    int DiaVencimento,
    DateOnly DataContratacao,
    DateOnly? DataFim,
    StatusContrato Status,
    decimal ValorPlano,
    decimal ValorPorDependente,
    decimal ValorTotal,
    int CarenciaDias,
    Guid? IdConsultor,
    IReadOnlyList<PessoaCobertaDetalheResponse> PessoasCobertas,
    string? MotivoCancelamento);

public sealed record TitularResponse(Guid IdCliente, string Nome, string Cpf);

/// <param name="IdPessoaCoberta">Identificador da pessoa no contrato (usado no registro do beneficiário).</param>
/// <param name="Tipo">TITULAR ou DEPENDENTE.</param>
/// <param name="IdDependente">Preenchido quando a pessoa é um dependente.</param>
/// <param name="Nome">Nome atual da pessoa no cadastro do cliente.</param>
/// <param name="Cpf">CPF da pessoa.</param>
/// <param name="DataInclusao">Data em que a pessoa entrou no contrato.</param>
/// <param name="CarenciaCumpridaEm">Data de inclusão mais a carência do plano na contratação (CON-04).</param>
public sealed record PessoaCobertaDetalheResponse(
    Guid IdPessoaCoberta,
    TipoPessoaCoberta Tipo,
    Guid? IdDependente,
    string Nome,
    string Cpf,
    DateOnly DataInclusao,
    DateOnly CarenciaCumpridaEm);

public sealed class ListarMeusContratosQueryHandler(IContratoRepository contratos, IClientesApi clientes, ICurrentUser usuario)
    : IQueryHandler<ListarMeusContratosQuery, IReadOnlyList<ContratoResumoResponse>>
{
    public async ValueTask<Result<IReadOnlyList<ContratoResumoResponse>>> Handle(ListarMeusContratosQuery query, CancellationToken cancellationToken)
    {
        if (usuario.IdCliente is not Guid idCliente)
        {
            return ContratoErros.TitularNaoEncontrado;
        }

        TitularDto? titular = await clientes.ObterTitularAsync(idCliente, cancellationToken);
        IReadOnlyList<Contrato> lista = await contratos.ListarDoClienteAsync(idCliente, cancellationToken);

        return lista
            .Select(c => new ContratoResumoResponse(
                c.Id, c.IdCliente, titular?.Nome ?? string.Empty, c.IdPlano, c.NomePlano, c.TipoCobertura, c.Periodicidade,
                c.Status, c.ValorTotal, c.DataContratacao, c.PessoasCobertas.Count(p => p.DataRemocao is null)))
            .ToList();
    }
}

public sealed class ObterContratoQueryHandler(IContratoRepository contratos, IClientesApi clientes, ICurrentUser usuario)
    : IQueryHandler<ObterContratoQuery, ContratoDetalheResponse>
{
    public async ValueTask<Result<ContratoDetalheResponse>> Handle(ObterContratoQuery query, CancellationToken cancellationToken)
    {
        Contrato? contrato = await contratos.ObterAsync(query.IdContrato, cancellationToken);

        // Cliente pedindo contrato de outro recebe 404, como se o contrato não existisse (CON-01).
        bool doCliente = usuario.Perfil != Perfis.Cliente || contrato?.IdCliente == usuario.IdCliente;
        if (contrato is null || !doCliente)
        {
            return ContratoErros.NaoEncontrado;
        }

        TitularDto? titular = await clientes.ObterTitularAsync(contrato.IdCliente, cancellationToken);
        PessoaCoberta[] cobertas = [.. contrato.PessoasNaVigencia()];
        Dictionary<Guid, DependenteDto> dependentes = (await clientes.ObterDependentesAsync(
                [.. cobertas.Where(p => p.IdDependente is not null).Select(p => p.IdDependente!.Value)], cancellationToken))
            .ToDictionary(d => d.IdDependente);

        PessoaCobertaDetalheResponse Pessoa(PessoaCoberta p)
        {
            (string nome, string cpf) = p.IdDependente is Guid id && dependentes.TryGetValue(id, out DependenteDto? d)
                ? (d.Nome, d.Cpf)
                : p.Tipo == TipoPessoaCoberta.Titular ? (titular?.Nome ?? string.Empty, titular?.Cpf ?? string.Empty) : (string.Empty, string.Empty);
            return new PessoaCobertaDetalheResponse(
                p.Id, p.Tipo, p.IdDependente, nome, cpf, p.DataInclusao, p.DataInclusao.AddDays(contrato.CarenciaDias));
        }

        return new ContratoDetalheResponse(
            contrato.Id,
            new TitularResponse(contrato.IdCliente, titular?.Nome ?? string.Empty, titular?.Cpf ?? string.Empty),
            contrato.IdPlano,
            contrato.NomePlano,
            contrato.TipoCobertura,
            contrato.Periodicidade,
            contrato.DiaVencimento,
            contrato.DataContratacao,
            contrato.DataFim,
            contrato.Status,
            contrato.ValorPlano,
            contrato.ValorPorDependente,
            contrato.ValorTotal,
            contrato.CarenciaDias,
            contrato.IdConsultor,
            [.. cobertas.OrderBy(p => p.Tipo).ThenBy(p => p.DataInclusao).Select(Pessoa)],
            contrato.MotivoCancelamento);
    }
}
