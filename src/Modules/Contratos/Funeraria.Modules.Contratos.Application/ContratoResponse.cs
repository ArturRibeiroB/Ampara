using Funeraria.Modules.Contratos.Domain.Contratos;

namespace Funeraria.Modules.Contratos.Application;

/// <summary>Contrato criado ou consultado (card Criar Contrato, CON-01).</summary>
public sealed record ContratoResponse(
    Guid IdContrato,
    Guid IdCliente,
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
    Guid? IdConsultor,
    IReadOnlyList<PessoaCobertaResponse> PessoasCobertas)
{
    public static ContratoResponse De(Contrato c) => new(
        c.Id,
        c.IdCliente,
        c.IdPlano,
        c.NomePlano,
        c.TipoCobertura,
        c.Periodicidade,
        c.DiaVencimento,
        c.DataContratacao,
        c.DataFim,
        c.Status,
        c.ValorPlano,
        c.ValorPorDependente,
        c.ValorTotal,
        c.IdConsultor,
        [.. c.PessoasCobertas.Where(p => p.DataRemocao is null).Select(p => new PessoaCobertaResponse(p.Id, p.Tipo, p.IdDependente, p.DataInclusao))]);
}

public sealed record PessoaCobertaResponse(Guid IdPessoaCoberta, TipoPessoaCoberta Tipo, Guid? IdDependente, DateOnly DataInclusao);
