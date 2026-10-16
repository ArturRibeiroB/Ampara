using Microsoft.AspNetCore.Identity;

namespace Funeraria.Modules.Identidade.Infrastructure.Usuarios;

/// <summary>
/// Conta de usuário do ASP.NET Core Identity (AUT-01): e-mail único na plataforma, hash da senha e bloqueio.
/// Fica na Infrastructure porque herda de <see cref="IdentityUser{TKey}"/>; o Domain não depende do Identity.
/// Os dados de negócio (cliente, funcionário, terceiro) ficam nos módulos de cada um, ligados ao id da conta.
/// </summary>
public sealed class Usuario : IdentityUser<Guid>
{
    public string Nome { get; set; } = null!;

    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; }
}
