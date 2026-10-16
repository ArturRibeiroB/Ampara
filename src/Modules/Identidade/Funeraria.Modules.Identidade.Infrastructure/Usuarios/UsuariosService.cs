using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.Modules.Identidade.Application.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace Funeraria.Modules.Identidade.Infrastructure.Usuarios;

internal sealed class UsuariosService(UserManager<Usuario> userManager) : IUsuarios
{
    public async Task<VerificacaoCredenciais> VerificarCredenciaisAsync(
        string email,
        string senha,
        CancellationToken cancellationToken = default)
    {
        Usuario? usuario = await userManager.FindByEmailAsync(email);
        if (usuario is null)
        {
            // Calcula um hash mesmo assim, para o tempo de resposta não revelar se o e-mail existe.
            userManager.PasswordHasher.HashPassword(new Usuario(), senha);
            return VerificacaoCredenciais.Invalido;
        }

        if (await userManager.IsLockedOutAsync(usuario))
        {
            return VerificacaoCredenciais.Bloqueado;
        }

        if (!await userManager.CheckPasswordAsync(usuario, senha))
        {
            await userManager.AccessFailedAsync(usuario);
            return await userManager.IsLockedOutAsync(usuario)
                ? VerificacaoCredenciais.Bloqueado
                : VerificacaoCredenciais.Invalido;
        }

        await userManager.ResetAccessFailedCountAsync(usuario);
        return new VerificacaoCredenciais(ResultadoCredenciais.Valido, await MapearAsync(usuario));
    }

    public async Task<UsuarioInfo?> ObterAsync(Guid idUsuario, CancellationToken cancellationToken = default)
    {
        Usuario? usuario = await userManager.FindByIdAsync(idUsuario.ToString());
        return usuario is null ? null : await MapearAsync(usuario);
    }

    private async Task<UsuarioInfo> MapearAsync(Usuario usuario) => new(
        usuario.Id,
        usuario.Nome,
        usuario.Email!,
        usuario.Ativo,
        await userManager.IsLockedOutAsync(usuario),
        await userManager.IsInRoleAsync(usuario, Perfis.Admin));
}
