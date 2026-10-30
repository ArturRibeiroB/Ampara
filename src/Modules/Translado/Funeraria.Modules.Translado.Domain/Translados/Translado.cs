using System.Globalization;
using System.Text;
using Funeraria.BuildingBlocks.Domain.Entities;
using Funeraria.BuildingBlocks.Domain.Estados;
using Funeraria.BuildingBlocks.Domain.Events;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.BuildingBlocks.Domain.Tenancy;

namespace Funeraria.Modules.Translado.Domain.Translados;

/// <summary>
/// Translado do corpo, vinculado ao funeral (TRA-01). O tipo é calculado aqui, a partir de origem e destino. O valor fica
/// gravado: alterar a tabela de preços depois não muda translados já registrados (TRA-02). Quem executa é sempre uma empresa
/// terceirizada (TRA-04, DEC-05).
/// </summary>
public sealed class Translado : AgregadoComStatus<StatusTranslado, AcaoTranslado>, ITenantEntity, IAuditableEntity
{
    private Translado(Guid id, Guid tenantId)
        : base(id, StatusTranslado.Agendado)
    {
        TenantId = tenantId;
    }

    private Translado()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid IdFuneral { get; private set; }

    public Guid IdBeneficiario { get; private set; }

    /// <summary>Titular do contrato, que acompanha o translado.</summary>
    public Guid IdCliente { get; private set; }

    public string NomeFalecido { get; private set; } = null!;

    public string ContatoNome { get; private set; } = null!;

    public string ContatoTelefone { get; private set; } = null!;

    public Endereco Origem { get; private set; } = null!;

    public Endereco Destino { get; private set; } = null!;

    public TipoTranslado Tipo { get; private set; }

    /// <summary>Data e hora previstas (UTC).</summary>
    public DateTime DataPrevista { get; private set; }

    public decimal DistanciaKm { get; private set; }

    /// <summary>A quilometragem foi informada pelo funcionário, e não calculada pela API de distância.</summary>
    public bool DistanciaManual { get; private set; }

    public decimal Valor { get; private set; }

    public decimal ValorCobertoPlano { get; private set; }

    public Guid? IdTerceiroFrota { get; private set; }

    public string? NomeFrota { get; private set; }

    /// <summary>Atribuição no portal da empresa (TER-02).</summary>
    public Guid? IdAtribuicao { get; private set; }

    public StatusAceite? StatusAceite { get; private set; }

    public string? MotivoRecusa { get; private set; }

    public string? MotoristaNome { get; private set; }

    public string? MotoristaTelefone { get; private set; }

    public string? VeiculoPlaca { get; private set; }

    public string? VeiculoModelo { get; private set; }

    protected override MaquinaDeEstados<StatusTranslado, AcaoTranslado> Maquina => EstadosTranslado.Maquina;

    public bool Encerrado => Status is StatusTranslado.Entregue or StatusTranslado.Cancelado;

    public static Result<Translado> Registrar(
        Guid tenantId, DadosFuneral funeral, Endereco origem, Endereco destino, DateTime dataPrevista, Cotacao cotacao, bool distanciaManual, DateTime agora, Guid? idUsuario)
    {
        Result<TipoTranslado> tipo = TiposTranslado.Calcular(origem, destino);
        if (tipo.IsFailure)
        {
            return tipo.Error;
        }

        var translado = new Translado(Guid.CreateVersion7(), tenantId)
        {
            IdFuneral = funeral.IdFuneral,
            IdBeneficiario = funeral.IdBeneficiario,
            IdCliente = funeral.IdCliente,
            NomeFalecido = funeral.NomeFalecido,
            ContatoNome = funeral.ContatoNome,
            ContatoTelefone = funeral.ContatoTelefone,
            Origem = origem,
            Destino = destino,
            Tipo = tipo.Value,
            DataPrevista = dataPrevista,
            DistanciaKm = cotacao.DistanciaKm,
            DistanciaManual = distanciaManual,
            Valor = cotacao.Valor,
            ValorCobertoPlano = cotacao.ValorCoberto
        };

        translado.RegistrarInicio(agora, idUsuario);
        return translado;
    }

    /// <returns>A atribuição da empresa anterior, para encerrar no portal dela.</returns>
    public Result<Guid?> AtribuirFrota(Guid idTerceiro, string nome)
    {
        if (Status != StatusTranslado.Agendado)
        {
            return TransladoErros.NaoEstaAgendado;
        }

        Guid? anterior = IdAtribuicao;
        IdTerceiroFrota = idTerceiro;
        NomeFrota = nome;
        IdAtribuicao = null;
        StatusAceite = Translados.StatusAceite.Pendente;
        MotivoRecusa = null;
        LimparMotorista();
        return anterior;
    }

    public void VincularAtribuicao(Guid idAtribuicao) => IdAtribuicao = idAtribuicao;

    public void RegistrarAceite() => StatusAceite = Translados.StatusAceite.Aceito;

    /// <summary>A recusa devolve o translado para nova atribuição (TRA-04).</summary>
    public void RegistrarRecusa(string motivo)
    {
        IdTerceiroFrota = null;
        NomeFrota = null;
        IdAtribuicao = null;
        StatusAceite = Translados.StatusAceite.Recusado;
        MotivoRecusa = motivo;
        LimparMotorista();
    }

    public Result InformarMotorista(string nome, string telefone, string placa, string modelo)
    {
        if (StatusAceite != Translados.StatusAceite.Aceito)
        {
            return TransladoErros.SemAceite;
        }

        MotoristaNome = nome.Trim();
        MotoristaTelefone = telefone.Trim();
        VeiculoPlaca = placa.Trim().ToUpperInvariant();
        VeiculoModelo = modelo.Trim();
        return Result.Success();
    }

    /// <summary>Muda para o status pedido (TRA-05). A coleta só começa com uma empresa que aceitou o serviço.</summary>
    public Result MudarPara(StatusTranslado destino, DateTime agora, Guid? idUsuario, string? observacao)
    {
        AcaoTranslado? acao = destino switch
        {
            StatusTranslado.EmColeta => AcaoTranslado.IniciarColeta,
            StatusTranslado.EmTransito => AcaoTranslado.IniciarViagem,
            StatusTranslado.Entregue => AcaoTranslado.ConfirmarEntrega,
            StatusTranslado.Cancelado => AcaoTranslado.Cancelar,
            _ => null
        };
        if (acao is null)
        {
            return TransladoErros.TransicaoInvalida(Status, destino);
        }

        if (destino == StatusTranslado.EmColeta && StatusAceite != Translados.StatusAceite.Aceito)
        {
            return TransladoErros.SemAceite;
        }

        return Executar(acao.Value, agora, idUsuario, observacao);
    }

    protected override void AoMudarStatus(StatusTranslado anterior, StatusTranslado atual, AcaoTranslado acao) =>
        Raise(new TransladoStatusAlteradoDomainEvent(Id, atual, Historico.LastOrDefault()?.Observacao));

    private void LimparMotorista()
    {
        MotoristaNome = null;
        MotoristaTelefone = null;
        VeiculoPlaca = null;
        VeiculoModelo = null;
    }
}

public sealed record Endereco(string Logradouro, string Cidade, string Uf, string Pais)
{
    public string Resumo => $"{Logradouro}, {Cidade}/{Uf}";
}

/// <summary>O que o translado usa do funeral.</summary>
public sealed record DadosFuneral(Guid IdFuneral, Guid IdBeneficiario, Guid IdCliente, string NomeFalecido, string ContatoNome, string ContatoTelefone);

public enum TipoTranslado
{
    Municipal = 1,
    Intermunicipal = 2,
    Interestadual = 3
}

public enum StatusAceite
{
    Pendente = 1,
    Aceito = 2,
    Recusado = 3
}

public static class TiposTranslado
{
    /// <summary>O tipo é definido aqui, não pelo frontend. Por enquanto, só dentro do Brasil (DEC-05).</summary>
    public static Result<TipoTranslado> Calcular(Endereco origem, Endereco destino)
    {
        if (Normalizar(origem.Pais) != "BR" || Normalizar(destino.Pais) != "BR")
        {
            return TransladoErros.Internacional;
        }

        if (Normalizar(origem.Uf) != Normalizar(destino.Uf))
        {
            return TipoTranslado.Interestadual;
        }

        return Normalizar(origem.Cidade) == Normalizar(destino.Cidade) ? TipoTranslado.Municipal : TipoTranslado.Intermunicipal;
    }

    /// <summary>Sem acento, sem diferença de caixa e sem espaços nas pontas: "São Paulo" e "SAO PAULO " são a mesma cidade.</summary>
    public static string Normalizar(string texto)
    {
        var limpo = new StringBuilder();
        foreach (char c in texto.Trim().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                limpo.Append(char.ToUpperInvariant(c));
            }
        }

        return limpo.ToString();
    }
}

/// <summary>Preço de um translado.</summary>
/// <param name="DistanciaKm">Distância total.</param>
/// <param name="Valor">Preço da distância total.</param>
/// <param name="ValorCoberto">Parte paga pelo plano.</param>
public sealed record Cotacao(decimal DistanciaKm, decimal Valor, decimal ValorCoberto)
{
    public decimal ValorAPagar => Valor - ValorCoberto;
}

/// <summary>Faixa da tabela de preços. <c>AteKm</c> vazio é a última faixa, aberta, com o valor por km excedente.</summary>
public sealed record FaixaKm(int DeKm, int? AteKm, decimal Valor, decimal? ValorKmExcedente = null);

/// <summary>
/// Configuração do translado de cada funerária: tabela de preços (TRA-02, DEC-05) e documentos exigidos por tipo (TRA-03).
/// </summary>
public sealed class ConfiguracaoTranslado : AggregateRoot<Guid>, ITenantEntity, IAuditableEntity
{
    private ConfiguracaoTranslado(Guid id, Guid tenantId)
        : base(id)
    {
        TenantId = tenantId;
    }

    private ConfiguracaoTranslado()
    {
    }

    public Guid TenantId { get; private set; }

    public IReadOnlyList<FaixaKm> Faixas { get; private set; } = [];

    /// <summary>Tipos de documento (formato da API, CER-01) exigidos por tipo de translado.</summary>
    public IReadOnlyDictionary<TipoTranslado, IReadOnlyList<string>> DocumentosExigidos { get; private set; } = PadraoDocumentos;

    /// <summary>Ponto de partida: a lista exata ainda precisa ser confirmada pelo grupo (TRA-03), por isso é editável.</summary>
    public static IReadOnlyDictionary<TipoTranslado, IReadOnlyList<string>> PadraoDocumentos { get; } =
        new Dictionary<TipoTranslado, IReadOnlyList<string>>
        {
            [TipoTranslado.Municipal] = ["DECLARACAO_OBITO"],
            [TipoTranslado.Intermunicipal] = ["DECLARACAO_OBITO", "DOCUMENTO_FALECIDO"],
            [TipoTranslado.Interestadual] = ["DECLARACAO_OBITO", "DOCUMENTO_FALECIDO", "CERTIDAO_OBITO"]
        };

    public static ConfiguracaoTranslado Criar(Guid tenantId) => new(Guid.CreateVersion7(), tenantId);

    public Result DefinirFaixas(IReadOnlyList<FaixaKm> faixas)
    {
        Result valida = TabelaKm.Validar(faixas);
        if (valida.IsSuccess)
        {
            Faixas = [.. faixas];
        }

        return valida;
    }

    public void DefinirDocumentos(IReadOnlyDictionary<TipoTranslado, IReadOnlyList<string>> documentos) => DocumentosExigidos = documentos;

    public IReadOnlyList<string> DocumentosDe(TipoTranslado tipo) => DocumentosExigidos.GetValueOrDefault(tipo) ?? [];
}

public static class TabelaKm
{
    /// <summary>Faixas contínuas a partir de 0 km, sem sobreposição nem buraco, terminando numa faixa aberta (TRA-02).</summary>
    public static Result Validar(IReadOnlyList<FaixaKm> faixas)
    {
        if (faixas.Count == 0 || faixas[0].DeKm != 0)
        {
            return TransladoErros.FaixasInvalidas("a primeira faixa precisa começar em 0 km.");
        }

        for (int i = 0; i < faixas.Count; i++)
        {
            FaixaKm faixa = faixas[i];
            bool ultima = i == faixas.Count - 1;
            if (faixa.Valor < 0 || faixa.ValorKmExcedente < 0)
            {
                return TransladoErros.FaixasInvalidas("os valores não podem ser negativos.");
            }

            if (ultima != (faixa.AteKm is null))
            {
                return TransladoErros.FaixasInvalidas("só a última faixa fica aberta, e ela é obrigatória para precificar qualquer distância.");
            }

            if (faixa.AteKm < faixa.DeKm)
            {
                return TransladoErros.FaixasInvalidas($"a faixa de {faixa.DeKm} km termina antes de começar.");
            }

            if (!ultima && faixas[i + 1].DeKm != faixa.AteKm + 1)
            {
                return TransladoErros.FaixasInvalidas($"depois de {faixa.AteKm} km, a próxima faixa precisa começar em {faixa.AteKm + 1} km (sem sobreposição nem buraco).");
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Se a distância couber nos km do plano, o custo é zero; acima disso, a família paga a diferença entre o preço da distância
    /// total e o preço dos km incluídos (regra do TRA-02, a confirmar pelo grupo).
    /// </summary>
    public static Cotacao Cotar(IReadOnlyList<FaixaKm> faixas, decimal distanciaKm, int kmIncluidos)
    {
        decimal valor = Preco(faixas, distanciaKm);
        decimal coberto = distanciaKm <= kmIncluidos ? valor : kmIncluidos > 0 ? Preco(faixas, kmIncluidos) : 0;
        return new Cotacao(distanciaKm, valor, coberto);
    }

    /// <summary>A quilometragem fracionada conta como o km seguinte: 50,2 km já é a faixa de 51 km.</summary>
    private static decimal Preco(IReadOnlyList<FaixaKm> faixas, decimal distanciaKm)
    {
        int km = (int)Math.Ceiling(distanciaKm);
        FaixaKm faixa = faixas.First(f => km >= f.DeKm && (f.AteKm is null || km <= f.AteKm));
        decimal excedente = faixa.AteKm is null ? (km - (faixa.DeKm - 1)) * (faixa.ValorKmExcedente ?? 0) : 0;
        return decimal.Round(faixa.Valor + excedente, 2, MidpointRounding.AwayFromZero);
    }
}

/// <summary>Distância já calculada: a mesma origem e destino não é consultada duas vezes (DEC-10). Vale para todas as funerárias.</summary>
public sealed class DistanciaCalculada : Entity<Guid>
{
    private DistanciaCalculada(Guid id)
        : base(id)
    {
    }

    private DistanciaCalculada()
    {
    }

    public string Chave { get; private set; } = null!;

    public decimal Km { get; private set; }

    public static DistanciaCalculada Criar(string chave, decimal km) => new(Guid.CreateVersion7()) { Chave = chave, Km = km };

    public static string ChaveDe(Endereco origem, Endereco destino) =>
        TiposTranslado.Normalizar($"{origem.Logradouro}|{origem.Cidade}|{origem.Uf}>{destino.Logradouro}|{destino.Cidade}|{destino.Uf}");
}

public sealed record TransladoStatusAlteradoDomainEvent(Guid IdTranslado, StatusTranslado Status, string? Observacao) : DomainEvent;

public static class TransladoErros
{
    public static readonly Error NaoEncontrado = Error.NaoEncontrado("Translado.NaoEncontrado", "Translado não encontrado.");

    public static readonly Error FuneralNaoEncontrado = Error.NaoEncontrado("Translado.FuneralNaoEncontrado", "Funeral não encontrado.");

    public static readonly Error FuneralEncerrado = Error.Conflito("Translado.FuneralEncerrado", "Funeral concluído ou cancelado não recebe translado.");

    public static readonly Error Internacional = Error.Validacao("Translado.Internacional", "Translado internacional ainda não está disponível.");

    public static readonly Error TabelaNaoConfigurada =
        Error.Conflito("Translado.TabelaNaoConfigurada", "Configure a tabela de preços do translado antes de registrar.");

    public static readonly Error DistanciaObrigatoria =
        Error.Validacao("Translado.DistanciaObrigatoria", "Não foi possível calcular a distância. Informe a quilometragem em distancia_km.");

    public static readonly Error NaoEstaAgendado = Error.Conflito("Translado.NaoEstaAgendado", "A empresa só pode ser trocada enquanto o translado está agendado.");

    public static readonly Error FrotaInvalida = Error.Validacao(
        "Translado.FrotaInvalida", "A empresa precisa ser um terceiro do tipo FROTA, ativo e que atende a região de origem.");

    public static readonly Error SemAceite = Error.Conflito("Translado.SemAceite", "A empresa de translado ainda não aceitou o serviço.");

    public static Error DocumentosFaltando(IEnumerable<string> faltando) =>
        Error.Conflito("Translado.DocumentosFaltando", $"Anexe os documentos obrigatórios antes de iniciar: {string.Join(", ", faltando)}.");

    public static Error FaixasInvalidas(string motivo) => Error.Validacao("Translado.FaixasInvalidas", $"Tabela de preços inválida: {motivo}");

    public static Error TransicaoInvalida(StatusTranslado atual, StatusTranslado destino) =>
        Error.Validacao("Estado.TransicaoInvalida", $"Não é possível mudar de {NomeApi.De(atual)} para {NomeApi.De(destino)}.");
}

public interface ITransladoRepository
{
    Task<Translado?> ObterAsync(Guid idTranslado, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Translado>> ListarDoFuneralAsync(Guid idFuneral, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Translado>> ListarDaFrotaAsync(Guid idTerceiro, CancellationToken cancellationToken = default);

    Task<ConfiguracaoTranslado?> ObterConfiguracaoAsync(CancellationToken cancellationToken = default);

    void AdicionarConfiguracao(ConfiguracaoTranslado configuracao);

    Task<decimal?> ObterDistanciaAsync(string chave, CancellationToken cancellationToken = default);

    void AdicionarDistancia(DistanciaCalculada distancia);

    void Adicionar(Translado translado);
}
