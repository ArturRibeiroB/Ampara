using Funeraria.BuildingBlocks.Domain.Estados;

namespace Funeraria.Modules.Translado.Domain.Translados;

public enum StatusTranslado
{
    Agendado = 1,
    EmColeta = 2,
    EmTransito = 3,
    Entregue = 4,
    Cancelado = 5
}

public enum AcaoTranslado
{
    IniciarColeta = 1,
    IniciarViagem = 2,
    ConfirmarEntrega = 3,
    Cancelar = 4
}

/// <summary>Transições do translado (ARQ-01, TRA-05).</summary>
public static class EstadosTranslado
{
    public static readonly MaquinaDeEstados<StatusTranslado, AcaoTranslado> Maquina = new MaquinaDeEstados<StatusTranslado, AcaoTranslado>()
        .Permitir(AcaoTranslado.IniciarColeta, StatusTranslado.EmColeta, StatusTranslado.Agendado)
        .Permitir(AcaoTranslado.IniciarViagem, StatusTranslado.EmTransito, StatusTranslado.EmColeta)
        .Permitir(AcaoTranslado.ConfirmarEntrega, StatusTranslado.Entregue, StatusTranslado.EmTransito)
        .Permitir(AcaoTranslado.Cancelar, StatusTranslado.Cancelado, StatusTranslado.Agendado, StatusTranslado.EmColeta, StatusTranslado.EmTransito);
}
