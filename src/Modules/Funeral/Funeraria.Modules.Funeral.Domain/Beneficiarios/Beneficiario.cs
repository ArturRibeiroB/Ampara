using Funeraria.BuildingBlocks.Domain.Entities;
using Funeraria.BuildingBlocks.Domain.Events;
using Funeraria.BuildingBlocks.Domain.Lgpd;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.BuildingBlocks.Domain.Tenancy;

namespace Funeraria.Modules.Funeral.Domain.Beneficiarios;

/// <summary>
/// Pessoa coberta que faleceu e vai usar o plano (card Gerar Beneficiário). Aponta para a pessoa no contrato
/// (<c>id_pessoa_coberta</c>, AJU-01), porque titular e dependente ficam em tabelas diferentes. Nome e nascimento são
/// copiados do cadastro na hora do registro, para o atendimento e a certidão (CER) não dependerem de alterações depois.
/// </summary>
public sealed class Beneficiario : AggregateRoot<Guid>, ITenantEntity, IAuditableEntity
{
    private Beneficiario(Guid id, Guid tenantId)
        : base(id)
    {
        TenantId = tenantId;
    }

    private Beneficiario()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid IdPessoaCoberta { get; private set; }

    public Guid IdContrato { get; private set; }

    /// <summary>Titular do contrato: acompanha o atendimento (DEC-06).</summary>
    public Guid IdCliente { get; private set; }

    /// <summary>Preenchido quando o falecido é um dependente.</summary>
    public Guid? IdDependente { get; private set; }

    public string Nome { get; private set; } = null!;

    public DateOnly DataNascimento { get; private set; }

    public DateOnly DataFalecimento { get; private set; }

    public string LocalFalecimento { get; private set; } = null!;

    /// <summary>Quem responde pela família no atendimento (contato dos terceiros no translado, TER-02).</summary>
    public ResponsavelFamilia Responsavel { get; private set; } = null!;

    /// <summary>Quando os dados do falecido e do responsável foram anonimizados (SEG-01). Nulo enquanto valem.</summary>
    public DateTime? AnonimizadoEm { get; private set; }

    /// <summary>
    /// Política de retenção (SEG-01): troca nome, local do falecimento e contato do responsável por valores sem relação com
    /// as pessoas. A data do falecimento fica, para os relatórios. Os outros módulos apagam documentos e cópias do nome pelo
    /// evento. Idempotente.
    /// </summary>
    /// <param name="agora">Momento da anonimização (UTC).</param>
    /// <param name="idsFunerais">Funerais do beneficiário, já anonimizados por quem chama.</param>
    public void Anonimizar(DateTime agora, IReadOnlyList<Guid> idsFunerais)
    {
        if (AnonimizadoEm is not null)
        {
            return;
        }

        Nome = Anonimizacao.Nome;
        DataNascimento = Anonimizacao.DataDeNascimento(DataNascimento);
        LocalFalecimento = Anonimizacao.Texto;
        Responsavel = new ResponsavelFamilia(Anonimizacao.Nome, string.Empty, Responsavel.Parentesco);
        AnonimizadoEm = agora;
        Raise(new BeneficiarioAnonimizadoDomainEvent(Id, idsFunerais));
    }

    /// <summary>
    /// Registra o falecimento. A elegibilidade (CON-04) e a pessoa já foram conferidas pelo caso de uso; aqui ficam as
    /// regras das datas: o falecimento não pode ser no futuro nem antes do nascimento.
    /// </summary>
    public static Result<Beneficiario> Registrar(
        Guid tenantId,
        PessoaFalecida pessoa,
        DateOnly dataFalecimento,
        string localFalecimento,
        ResponsavelFamilia responsavel,
        DateOnly hoje)
    {
        if (dataFalecimento > hoje)
        {
            return BeneficiarioErros.FalecimentoNoFuturo;
        }

        if (dataFalecimento < pessoa.DataNascimento)
        {
            return BeneficiarioErros.FalecimentoAntesDoNascimento;
        }

        return new Beneficiario(Guid.CreateVersion7(), tenantId)
        {
            IdPessoaCoberta = pessoa.IdPessoaCoberta,
            IdContrato = pessoa.IdContrato,
            IdCliente = pessoa.IdCliente,
            IdDependente = pessoa.IdDependente,
            Nome = pessoa.Nome,
            DataNascimento = pessoa.DataNascimento,
            DataFalecimento = dataFalecimento,
            LocalFalecimento = localFalecimento.Trim(),
            Responsavel = responsavel with
            {
                Nome = responsavel.Nome.Trim(),
                Telefone = responsavel.Telefone.Trim(),
                Parentesco = responsavel.Parentesco.Trim()
            }
        };
    }
}

public sealed record BeneficiarioAnonimizadoDomainEvent(Guid IdBeneficiario, IReadOnlyList<Guid> IdsFunerais) : DomainEvent;

/// <summary>A pessoa coberta como está no contrato e no cadastro do cliente.</summary>
public sealed record PessoaFalecida(
    Guid IdPessoaCoberta, Guid IdContrato, Guid IdCliente, Guid? IdDependente, string Nome, DateOnly DataNascimento);

public sealed record ResponsavelFamilia(string Nome, string Telefone, string Parentesco);

public static class BeneficiarioErros
{
    public static readonly Error NaoEncontrado =
        Error.NaoEncontrado("Beneficiario.NaoEncontrado", "Beneficiário não encontrado.");

    public static readonly Error PessoaNaoEncontrada =
        Error.NaoEncontrado("Beneficiario.PessoaNaoEncontrada", "Pessoa coberta não encontrada nesta funerária.");

    public static readonly Error JaRegistrado =
        Error.Conflito("Beneficiario.JaRegistrado", "Esta pessoa já foi registrada como beneficiária.");

    public static Error NaoElegivel(string motivo) =>
        Error.Conflito("Beneficiario.NaoElegivel", $"A pessoa não pode usar o plano: {motivo}");

    public static readonly Error FalecimentoNoFuturo =
        Error.Validacao("Beneficiario.FalecimentoNoFuturo", "A data de falecimento não pode ser no futuro.");

    public static readonly Error FalecimentoAntesDoNascimento =
        Error.Validacao("Beneficiario.FalecimentoAntesDoNascimento", "A data de falecimento não pode ser anterior à data de nascimento.");
}

public interface IBeneficiarioRepository
{
    Task<Beneficiario?> ObterAsync(Guid idBeneficiario, CancellationToken cancellationToken = default);

    Task<bool> ExisteDaPessoaAsync(Guid idPessoaCoberta, CancellationToken cancellationToken = default);

    void Adicionar(Beneficiario beneficiario);
}
