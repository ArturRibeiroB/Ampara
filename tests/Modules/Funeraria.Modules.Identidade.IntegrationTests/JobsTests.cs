using System.Net;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Infrastructure.Jobs;
using Funeraria.Modules.Identidade.Domain.Senhas;
using Funeraria.Modules.Identidade.Domain.Sessoes;
using Funeraria.Modules.Identidade.Infrastructure.Database;
using Funeraria.Testing;
using Hangfire;
using Hangfire.Storage;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Identidade.IntegrationTests;

/// <summary>INF-11 (Hangfire), AUT-03 (limpeza de sessões) e INF-13 (saúde).</summary>
[Collection(ApiCollection.Nome)]
public sealed class JobsTests(IdentidadeApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Jobs_recorrentes_dos_modulos_ficam_agendados()
    {
        using IStorageConnection conexao = factory.Services.GetRequiredService<JobStorage>().GetConnection();

        string[] ids = conexao.GetRecurringJobs().Select(j => j.Id).ToArray();

        Assert.Contains("limpeza-outbox", ids);
        Assert.Contains("identidade-limpeza-sessoes", ids);
    }

    [Fact]
    public async Task Limpeza_apaga_so_tokens_vencidos_ou_usados_ha_mais_de_30_dias()
    {
        (Guid idUsuario, _) = await factory.CriarUsuarioAsync();
        DateTime agora = DateTime.UtcNow;

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IdentidadeDbContext>();
            context.AddRange(
                RefreshToken.Criar(idUsuario, null, Hash("antigo"), agora.AddDays(-60), TimeSpan.FromDays(7)),
                RefreshToken.Criar(idUsuario, null, Hash("recente"), agora, TimeSpan.FromDays(7)),
                TokenSenha.Criar(idUsuario, FinalidadeTokenSenha.DefinirSenha, Hash("convite-antigo"), agora.AddDays(-40), TimeSpan.FromDays(3)),
                TokenSenha.Criar(idUsuario, FinalidadeTokenSenha.DefinirSenha, Hash("convite-valido"), agora, TimeSpan.FromDays(3)));
            await context.SaveChangesAsync(Ct);
        }

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ExecutorJobRecorrente>().ExecutarAsync("identidade-limpeza-sessoes", Ct);
        }

        await using AsyncServiceScope verificacao = factory.Services.CreateAsyncScope();
        var db = verificacao.ServiceProvider.GetRequiredService<IdentidadeDbContext>();
        string[] refresh = await db.Set<RefreshToken>().Where(t => t.IdUsuario == idUsuario).Select(t => t.TokenHash).ToArrayAsync(Ct);
        string[] senha = await db.Set<TokenSenha>().Where(t => t.IdUsuario == idUsuario).Select(t => t.TokenHash).ToArrayAsync(Ct);

        Assert.Equal([Hash("recente")], refresh.Select(h => h.Trim()));
        Assert.Equal([Hash("convite-valido")], senha.Select(h => h.Trim()));
    }

    [Fact]
    public async Task Painel_de_jobs_so_abre_para_o_admin()
    {
        HttpClient semCookie = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Unauthorized, (await semCookie.GetAsync("/hangfire", Ct)).StatusCode);

        (Guid tenant, _) = await factory.CriarFunerariaAsync();
        HttpResponseMessage gestor = await semCookie.GetAsync($"/hangfire?access_token={TokenDeTeste.Gerar(Perfis.Funeraria, tenant)}", Ct);
        string cookieGestor = CookieDoPainel(gestor);
        Assert.Equal(HttpStatusCode.Forbidden, (await ComCookie(cookieGestor)).StatusCode);

        HttpResponseMessage admin = await semCookie.GetAsync($"/hangfire?access_token={TokenDeTeste.Gerar(Perfis.Admin, null)}", Ct);
        Assert.Equal(HttpStatusCode.Redirect, admin.StatusCode);
        Assert.Equal("/hangfire", admin.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await ComCookie(CookieDoPainel(admin))).StatusCode);
    }

    [Fact]
    public async Task Saude_inclui_banco_e_hangfire()
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync("/health/ready", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private Task<HttpResponseMessage> ComCookie(string cookie)
    {
        HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var request = new HttpRequestMessage(HttpMethod.Get, "/hangfire");
        request.Headers.Add("Cookie", cookie);
        return client.SendAsync(request, Ct);
    }

    private static string CookieDoPainel(HttpResponseMessage response)
    {
        string setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("funeraria_painel=", StringComparison.Ordinal));
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        return setCookie.Split(';')[0];
    }

    private static string Hash(string valor) =>
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(valor)));
}
