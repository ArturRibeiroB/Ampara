using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Identidade.Application.Abstractions;
using Funeraria.Modules.Identidade.Domain;
using Microsoft.AspNetCore.Identity;

namespace Funeraria.Modules.Identidade.Infrastructure.Usuarios;

internal sealed class UsuariosService(UserManager<Usuario> userManager, TimeProvider timeProvider) : IUsuarios
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

    public async Task<UsuarioInfo?> ObterPorEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        Usuario? usuario = await userManager.FindByEmailAsync(email.Trim());
        return usuario is null ? null : await MapearAsync(usuario);
    }

    public async Task<UsuarioInfo> CriarConvidadoAsync(string email, string nome, CancellationToken cancellationToken = default)
    {
        var usuario = new Usuario
        {
            Id = Guid.CreateVersion7(),
            UserName = email.Trim(),
            Email = email.Trim(),
            EmailConfirmed = false,
            Nome = nome.Trim(),
            CriadoEm = timeProvider.GetUtcNow().UtcDateTime
        };

        IdentityResult result = await userManager.CreateAsync(usuario);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Não foi possível criar a conta convidada: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }

        return await MapearAsync(usuario);
    }

    public async Task<Result<UsuarioInfo>> CriarComSenhaAsync(
        string email,
        string nome,
        string senha,
        CancellationToken cancellationToken = default)
    {
        var usuario = new Usuario
        {
            Id = Guid.CreateVersion7(),
            UserName = email.Trim(),
            Email = email.Trim(),
            EmailConfirmed = false,
            Nome = nome.Trim(),
            CriadoEm = timeProvider.GetUtcNow().UtcDateTime
        };

        // CreateAsync com senha valida a senha antes de gravar: senha fraca não deixa conta criada.
        IdentityResult result = await userManager.CreateAsync(usuario, senha);
        if (!result.Succeeded)
        {
            string[] erros = [.. result.Errors.Select(e => e.Description).Distinct()];
            return result.Errors.All(e => e.Code.StartsWith("Password", StringComparison.Ordinal))
                ? new ValidationError(new Dictionary<string, string[]> { ["senha"] = erros })
                : new ValidationError(new Dictionary<string, string[]> { ["email"] = erros });
        }

        return await MapearAsync(usuario);
    }

    public async Task<bool> PossuiSenhaAsync(Guid idUsuario, CancellationToken cancellationToken = default)
    {
        Usuario? usuario = await userManager.FindByIdAsync(idUsuario.ToString());
        return usuario is not null && await userManager.HasPasswordAsync(usuario);
    }

    public async Task<Result> DefinirSenhaAsync(Guid idUsuario, string senha, CancellationToken cancellationToken = default)
    {
        Usuario? usuario = await userManager.FindByIdAsync(idUsuario.ToString());
        if (usuario is null)
        {
            return IdentidadeErros.LinkSenhaInvalido;
        }

        // Valida antes de mexer na senha atual, para uma senha fraca não deixar a conta sem senha.
        var erros = new List<string>();
        foreach (IPasswordValidator<Usuario> validator in userManager.PasswordValidators)
        {
            IdentityResult validacao = await validator.ValidateAsync(userManager, usuario, senha);
            erros.AddRange(validacao.Errors.Select(e => e.Description));
        }

        if (erros.Count > 0)
        {
            return new ValidationError(new Dictionary<string, string[]> { ["senha"] = [.. erros.Distinct()] });
        }

        if (await userManager.HasPasswordAsync(usuario))
        {
            Garantir(await userManager.RemovePasswordAsync(usuario));
        }

        Garantir(await userManager.AddPasswordAsync(usuario, senha));

        usuario.EmailConfirmed = true;
        Garantir(await userManager.UpdateAsync(usuario));
        await userManager.ResetAccessFailedCountAsync(usuario);
        await userManager.SetLockoutEndDateAsync(usuario, null);

        return Result.Success();
    }

    public async Task ConfirmarEmailAsync(Guid idUsuario, CancellationToken cancellationToken = default)
    {
        Usuario usuario = await userManager.FindByIdAsync(idUsuario.ToString())
            ?? throw new InvalidOperationException($"Usuário {idUsuario} não encontrado.");
        if (!usuario.EmailConfirmed)
        {
            usuario.EmailConfirmed = true;
            Garantir(await userManager.UpdateAsync(usuario));
        }
    }

    private async Task<UsuarioInfo> MapearAsync(Usuario usuario) => new(
        usuario.Id,
        usuario.Nome,
        usuario.Email!,
        usuario.Ativo,
        await userManager.IsLockedOutAsync(usuario),
        await userManager.IsInRoleAsync(usuario, Perfis.Admin),
        usuario.EmailConfirmed);

    private static void Garantir(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}
