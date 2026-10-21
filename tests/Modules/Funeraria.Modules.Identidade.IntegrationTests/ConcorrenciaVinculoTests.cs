using Funeraria.Modules.Identidade.Contracts;
using Funeraria.Modules.Identidade.Domain.Vinculos;
using Funeraria.Modules.Identidade.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Identidade.IntegrationTests;

/// <summary>
/// O cadastro de cliente cria o vínculo na hora e o outbox cria de novo como garantia. As duas gravações podem
/// acontecer ao mesmo tempo: nenhuma pode falhar e só um vínculo pode existir.
/// </summary>
[Collection(ApiCollection.Nome)]
public sealed class ConcorrenciaVinculoTests(IdentidadeApiFactory factory)
{
    [Fact]
    public async Task Vincular_o_mesmo_cliente_em_paralelo_cria_um_unico_vinculo_sem_erro()
    {
        (Guid tenant, _) = await factory.CriarFunerariaAsync();
        (Guid idUsuario, _) = await factory.CriarUsuarioAsync();
        Guid idCliente = Guid.NewGuid();

        Task[] chamadas = Enumerable.Range(0, 12).Select(_ => Task.Run(async () =>
        {
            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IIdentidadeApi>()
                .VincularClienteAsync(idUsuario, tenant, idCliente, TestContext.Current.CancellationToken);
        })).ToArray();

        await Task.WhenAll(chamadas);

        await using AsyncServiceScope verificacao = factory.Services.CreateAsyncScope();
        int vinculos = await verificacao.ServiceProvider.GetRequiredService<IdentidadeDbContext>()
            .Set<UsuarioVinculo>()
            .CountAsync(v => v.IdUsuario == idUsuario && v.IdTenant == tenant, TestContext.Current.CancellationToken);
        Assert.Equal(1, vinculos);
    }
}
