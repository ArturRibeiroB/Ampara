namespace Funeraria.BuildingBlocks.Infrastructure.Endpoints;

/// <summary>Nomes das políticas de limite de requisições (SEG-02). Registradas no host.</summary>
public static class PoliticasLimite
{
    /// <summary>Auto-cadastro público de funerárias: poucas tentativas por IP (TEN-02).</summary>
    public const string Onboarding = nameof(Onboarding);

    /// <summary>Cadastro público de clientes na funerária (card Criar Cliente).</summary>
    public const string CadastroCliente = nameof(CadastroCliente);

    /// <summary>Login, definição e recuperação de senha: freia tentativa em massa de senhas (SEG-02).</summary>
    public const string Autenticacao = nameof(Autenticacao);
}
