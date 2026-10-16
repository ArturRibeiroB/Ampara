using Funeraria.Modules.Identidade.Domain.Vinculos;
using Funeraria.Modules.Identidade.Infrastructure.Database;
using Funeraria.Modules.Identidade.Infrastructure.Usuarios;
using Funeraria.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Identidade.IntegrationTests;

public sealed class IdentidadeApiFactory : FunerariaApiFactory, IAsyncLifetime
{
    public ValueTask InitializeAsync() => IniciarAsync();

    /// <summary>Cria uma conta com a <see cref="FunerariaApiFactory.SenhaPadrao"/> e os vínculos informados.</summary>
    public async Task<(Guid IdUsuario, string Email)> CriarUsuarioAsync(params (Guid IdTenant, PerfilVinculo Perfil)[] vinculos)
    {
        string email = $"{Guid.NewGuid():N}@teste.localhost";

        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var usuario = new Usuario
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            Nome = "Usuário de Teste",
            CriadoEm = DateTime.UtcNow
        };

        IdentityResult result = await userManager.CreateAsync(usuario, SenhaPadrao);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));

        var context = scope.ServiceProvider.GetRequiredService<IdentidadeDbContext>();
        foreach ((Guid idTenant, PerfilVinculo perfil) in vinculos)
        {
            context.Add(UsuarioVinculo.Criar(
                usuario.Id, idTenant, perfil,
                idCliente: perfil == PerfilVinculo.Cliente ? Guid.NewGuid() : null));
        }

        await context.SaveChangesAsync();
        return (usuario.Id, email);
    }

    public async Task DesativarUsuarioAsync(Guid idUsuario)
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        Usuario usuario = (await userManager.FindByIdAsync(idUsuario.ToString()))!;
        usuario.Ativo = false;
        await userManager.UpdateAsync(usuario);
    }

    public async Task DesativarVinculoAsync(Guid idUsuario, Guid idTenant)
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentidadeDbContext>();
        UsuarioVinculo vinculo = await context.Set<UsuarioVinculo>().SingleAsync(v => v.IdUsuario == idUsuario && v.IdTenant == idTenant);
        vinculo.AlterarStatus(false);
        await context.SaveChangesAsync();
    }
}

[CollectionDefinition(Nome)]
public sealed class ApiCollection : ICollectionFixture<IdentidadeApiFactory>
{
    public const string Nome = "Api";
}
