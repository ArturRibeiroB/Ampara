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
    public Task<(Guid IdUsuario, string Email)> CriarUsuarioAsync(params (Guid IdTenant, PerfilVinculo Perfil)[] vinculos) =>
        Cenarios.CriarUsuarioAsync(this, vinculos);

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
