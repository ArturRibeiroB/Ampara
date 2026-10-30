using Funeraria.BuildingBlocks.Domain.Estados;
using Funeraria.BuildingBlocks.Domain.Events;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Documentos.Domain.Certidoes;
using Funeraria.Modules.Funeral.Domain.Funerais;
using Funeraria.Modules.Translado.Domain.Translados;

namespace Funeraria.BuildingBlocks.UnitTests;

/// <summary>ARQ-01: tabela de transições, histórico e evento em cada mudança.</summary>
public sealed class MaquinaDeEstadosTests
{
    public static TheoryData<StatusFuneral, AcaoFuneral, StatusFuneral?> Funeral() => Combinacoes(new Dictionary<(StatusFuneral, AcaoFuneral), StatusFuneral>
    {
        [(StatusFuneral.Registrado, AcaoFuneral.IniciarPreparacao)] = StatusFuneral.EmPreparacao,
        [(StatusFuneral.EmPreparacao, AcaoFuneral.IniciarVelorio)] = StatusFuneral.Velorio,
        [(StatusFuneral.Velorio, AcaoFuneral.Concluir)] = StatusFuneral.Concluido,
        [(StatusFuneral.Registrado, AcaoFuneral.Cancelar)] = StatusFuneral.Cancelado,
        [(StatusFuneral.EmPreparacao, AcaoFuneral.Cancelar)] = StatusFuneral.Cancelado,
        [(StatusFuneral.Velorio, AcaoFuneral.Cancelar)] = StatusFuneral.Cancelado
    });

    public static TheoryData<StatusTranslado, AcaoTranslado, StatusTranslado?> Translado() => Combinacoes(new Dictionary<(StatusTranslado, AcaoTranslado), StatusTranslado>
    {
        [(StatusTranslado.Agendado, AcaoTranslado.IniciarColeta)] = StatusTranslado.EmColeta,
        [(StatusTranslado.EmColeta, AcaoTranslado.IniciarViagem)] = StatusTranslado.EmTransito,
        [(StatusTranslado.EmTransito, AcaoTranslado.ConfirmarEntrega)] = StatusTranslado.Entregue,
        [(StatusTranslado.Agendado, AcaoTranslado.Cancelar)] = StatusTranslado.Cancelado,
        [(StatusTranslado.EmColeta, AcaoTranslado.Cancelar)] = StatusTranslado.Cancelado,
        [(StatusTranslado.EmTransito, AcaoTranslado.Cancelar)] = StatusTranslado.Cancelado
    });

    public static TheoryData<StatusCertidao, AcaoCertidao, StatusCertidao?> Certidao() => Combinacoes(new Dictionary<(StatusCertidao, AcaoCertidao), StatusCertidao>
    {
        [(StatusCertidao.Solicitada, AcaoCertidao.EnviarParaApi)] = StatusCertidao.EmProcessamento,
        [(StatusCertidao.EmProcessamento, AcaoCertidao.ConfirmarEmissao)] = StatusCertidao.Emitida,
        [(StatusCertidao.EmProcessamento, AcaoCertidao.Rejeitar)] = StatusCertidao.Rejeitada,
        [(StatusCertidao.EmProcessamento, AcaoCertidao.Falhar)] = StatusCertidao.Erro,
        [(StatusCertidao.Erro, AcaoCertidao.Reenviar)] = StatusCertidao.Solicitada,
        [(StatusCertidao.Rejeitada, AcaoCertidao.Reenviar)] = StatusCertidao.Solicitada
    });

    [Theory]
    [MemberData(nameof(Funeral))]
    public void Funeral_so_aceita_as_transicoes_do_FUN06(StatusFuneral atual, AcaoFuneral acao, StatusFuneral? esperado) =>
        Conferir(EstadosFuneral.Maquina, atual, acao, esperado);

    [Theory]
    [MemberData(nameof(Translado))]
    public void Translado_so_aceita_as_transicoes_do_card(StatusTranslado atual, AcaoTranslado acao, StatusTranslado? esperado) =>
        Conferir(EstadosTranslado.Maquina, atual, acao, esperado);

    [Theory]
    [MemberData(nameof(Certidao))]
    public void Certidao_so_aceita_as_transicoes_do_card(StatusCertidao atual, AcaoCertidao acao, StatusCertidao? esperado) =>
        Conferir(EstadosCertidao.Maquina, atual, acao, esperado);

    [Fact]
    public void Transicao_invalida_explica_no_formato_da_api()
    {
        Result<StatusTranslado> resultado = EstadosTranslado.Maquina.Disparar(StatusTranslado.Entregue, AcaoTranslado.IniciarColeta);

        Assert.Equal("Estado.TransicaoInvalida", resultado.Error.Code);
        Assert.Equal("Não é possível INICIAR_COLETA quando o status é ENTREGUE.", resultado.Error.Description);
    }

    [Fact]
    public void Transicao_repetida_na_declaracao_e_erro_de_programacao() =>
        Assert.Throws<InvalidOperationException>(() => new MaquinaDeEstados<StatusFuneral, AcaoFuneral>()
            .Permitir(AcaoFuneral.Cancelar, StatusFuneral.Cancelado, StatusFuneral.Registrado)
            .Permitir(AcaoFuneral.Cancelar, StatusFuneral.Concluido, StatusFuneral.Registrado));

    [Fact]
    public void Agregado_grava_historico_e_evento_em_cada_transicao_e_nada_quando_recusa()
    {
        var agora = new DateTime(2026, 10, 26, 10, 0, 0, DateTimeKind.Utc);
        Guid usuario = Guid.NewGuid();
        var translado = new TransladoDeTeste(agora, usuario);

        Assert.True(translado.Executar(AcaoTranslado.IniciarColeta, agora.AddHours(1), usuario, "Motorista a caminho").IsSuccess);
        Assert.True(translado.Executar(AcaoTranslado.ConfirmarEntrega, agora.AddHours(2), usuario).IsFailure);

        Assert.Equal(StatusTranslado.EmColeta, translado.Status);
        Assert.Equal([AcaoTranslado.IniciarViagem, AcaoTranslado.Cancelar], translado.AcoesPermitidas.Order());
        Assert.Equal(2, translado.Historico.Count);
        HistoricoStatus<StatusTranslado> mudanca = translado.Historico[^1];
        Assert.Equal((StatusTranslado.Agendado, StatusTranslado.EmColeta, "INICIAR_COLETA"), (mudanca.De, mudanca.Para, mudanca.Acao));
        Assert.Equal("Motorista a caminho", mudanca.Observacao);
        Assert.Equal(usuario, mudanca.IdUsuario);
        Assert.Single(translado.DomainEvents.OfType<StatusDeTesteAlterado>());
    }

    private static TheoryData<TEstado, TAcao, TEstado?> Combinacoes<TEstado, TAcao>(Dictionary<(TEstado, TAcao), TEstado> validas)
        where TEstado : struct, Enum
        where TAcao : struct, Enum
    {
        var dados = new TheoryData<TEstado, TAcao, TEstado?>();
        foreach (TEstado estado in Enum.GetValues<TEstado>())
        {
            foreach (TAcao acao in Enum.GetValues<TAcao>())
            {
                dados.Add(estado, acao, validas.TryGetValue((estado, acao), out TEstado para) ? para : null);
            }
        }

        return dados;
    }

    private static void Conferir<TEstado, TAcao>(MaquinaDeEstados<TEstado, TAcao> maquina, TEstado atual, TAcao acao, TEstado? esperado)
        where TEstado : struct, Enum
        where TAcao : struct, Enum
    {
        Result<TEstado> resultado = maquina.Disparar(atual, acao);
        if (esperado is null)
        {
            Assert.True(resultado.IsFailure, $"{atual} --{acao}--> deveria ser recusada.");
        }
        else
        {
            Assert.True(resultado.IsSuccess, $"{atual} --{acao}--> deveria ser aceita.");
            Assert.Equal(esperado.Value, resultado.Value);
        }
    }

    private sealed record StatusDeTesteAlterado(StatusTranslado De, StatusTranslado Para) : DomainEvent;

    private sealed class TransladoDeTeste : AgregadoComStatus<StatusTranslado, AcaoTranslado>
    {
        public TransladoDeTeste(DateTime agora, Guid usuario)
            : base(Guid.NewGuid(), StatusTranslado.Agendado) => RegistrarInicio(agora, usuario);

        protected override MaquinaDeEstados<StatusTranslado, AcaoTranslado> Maquina => EstadosTranslado.Maquina;

        protected override void AoMudarStatus(StatusTranslado anterior, StatusTranslado atual, AcaoTranslado acao) =>
            Raise(new StatusDeTesteAlterado(anterior, atual));
    }
}
