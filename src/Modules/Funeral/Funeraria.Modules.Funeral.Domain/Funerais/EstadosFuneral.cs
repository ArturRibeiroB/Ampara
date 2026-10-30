using Funeraria.BuildingBlocks.Domain.Estados;

namespace Funeraria.Modules.Funeral.Domain.Funerais;

/// <summary>Etapas do atendimento (FUN-06): REGISTRADO → EM_PREPARACAO → VELORIO → CONCLUIDO, com cancelamento até o velório.</summary>
public enum StatusFuneral
{
    Registrado = 1,
    EmPreparacao = 2,
    Velorio = 3,
    Concluido = 4,
    Cancelado = 5
}

public enum AcaoFuneral
{
    IniciarPreparacao = 1,
    IniciarVelorio = 2,
    Concluir = 3,
    Cancelar = 4
}

/// <summary>Transições do funeral, declaradas num lugar só (ARQ-01, FUN-06).</summary>
public static class EstadosFuneral
{
    public static readonly MaquinaDeEstados<StatusFuneral, AcaoFuneral> Maquina = new MaquinaDeEstados<StatusFuneral, AcaoFuneral>()
        .Permitir(AcaoFuneral.IniciarPreparacao, StatusFuneral.EmPreparacao, StatusFuneral.Registrado)
        .Permitir(AcaoFuneral.IniciarVelorio, StatusFuneral.Velorio, StatusFuneral.EmPreparacao)
        .Permitir(AcaoFuneral.Concluir, StatusFuneral.Concluido, StatusFuneral.Velorio)
        .Permitir(AcaoFuneral.Cancelar, StatusFuneral.Cancelado, StatusFuneral.Registrado, StatusFuneral.EmPreparacao, StatusFuneral.Velorio);
}
