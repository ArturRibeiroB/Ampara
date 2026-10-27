using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Clientes.Contracts;
using Funeraria.Modules.Contratos.Domain.Contratos;

namespace Funeraria.Modules.Contratos.Application.Queries;

/// <summary>
/// Quem do contrato pode usar o plano hoje (CON-04): contrato ativo e adimplente e carência cumprida, por pessoa.
/// A equipe consulta no atendimento; o cliente, só nos próprios contratos.
/// </summary>
public sealed record ConsultarElegibilidadeQuery(Guid IdContrato) : IQuery<IReadOnlyList<ElegibilidadeResponse>>;

/// <param name="IdPessoaCoberta">Pessoa no contrato (é o id usado no registro do beneficiário).</param>
/// <param name="Tipo">TITULAR ou DEPENDENTE.</param>
/// <param name="IdDependente">Preenchido quando a pessoa é um dependente.</param>
/// <param name="Nome">Nome da pessoa.</param>
/// <param name="Elegivel">Pode usar o plano hoje.</param>
/// <param name="Motivo">Por que não pode; vazio quando pode.</param>
/// <param name="CarenciaCumpridaEm">Fim da carência: data de inclusão mais a carência do plano na contratação.</param>
public sealed record ElegibilidadeResponse(
    Guid IdPessoaCoberta,
    TipoPessoaCoberta Tipo,
    Guid? IdDependente,
    string Nome,
    bool Elegivel,
    string? Motivo,
    DateOnly CarenciaCumpridaEm);

public sealed class ConsultarElegibilidadeQueryHandler(
    IContratoRepository contratos,
    IClientesApi clientes,
    ICurrentUser usuario,
    TimeProvider timeProvider)
    : IQueryHandler<ConsultarElegibilidadeQuery, IReadOnlyList<ElegibilidadeResponse>>
{
    public async ValueTask<Result<IReadOnlyList<ElegibilidadeResponse>>> Handle(ConsultarElegibilidadeQuery query, CancellationToken cancellationToken)
    {
        Contrato? contrato = await contratos.ObterAsync(query.IdContrato, cancellationToken);
        if (contrato is null || (usuario.Perfil == Perfis.Cliente && contrato.IdCliente != usuario.IdCliente))
        {
            return Result.Failure<IReadOnlyList<ElegibilidadeResponse>>(ContratoErros.NaoEncontrado);
        }

        DateOnly hoje = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        TitularDto? titular = await clientes.ObterTitularAsync(contrato.IdCliente, cancellationToken);
        PessoaCoberta[] pessoas = [.. contrato.PessoasCobertas.Where(p => p.DataRemocao is null)];
        Dictionary<Guid, string> dependentes = (await clientes.ObterDependentesAsync(
                [.. pessoas.Where(p => p.IdDependente is not null).Select(p => p.IdDependente!.Value)], cancellationToken))
            .ToDictionary(d => d.IdDependente, d => d.Nome);

        return pessoas
            .OrderBy(p => p.Tipo).ThenBy(p => p.DataInclusao)
            .Select(p =>
            {
                Elegibilidade e = contrato.AvaliarElegibilidade(p, hoje);
                string nome = p.IdDependente is Guid id ? dependentes.GetValueOrDefault(id, string.Empty) : titular?.Nome ?? string.Empty;
                return new ElegibilidadeResponse(p.Id, p.Tipo, p.IdDependente, nome, e.Elegivel, e.Motivo, e.CarenciaCumpridaEm);
            })
            .ToList();
    }
}
