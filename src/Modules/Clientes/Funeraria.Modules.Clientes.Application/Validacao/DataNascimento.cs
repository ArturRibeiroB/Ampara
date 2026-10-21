namespace Funeraria.Modules.Clientes.Application.Validacao;

internal static class DataNascimento
{
    /// <summary>Data informada, não futura e de no máximo 130 anos atrás.</summary>
    public static bool Valida(DateOnly? data, TimeProvider timeProvider)
    {
        if (data is not DateOnly d)
        {
            return false;
        }

        DateOnly hoje = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        return d <= hoje && d >= hoje.AddYears(-130);
    }
}
