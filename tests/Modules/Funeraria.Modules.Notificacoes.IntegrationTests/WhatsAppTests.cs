using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Infrastructure.Tenancy;
using Funeraria.Modules.Notificacoes.Contracts;
using Funeraria.Modules.Notificacoes.Infrastructure.WhatsApp;
using Funeraria.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Notificacoes.IntegrationTests;

/// <summary>WhatsApp ligado com a fake e dois templates, como ficaria em produção depois de aprovados.</summary>
public sealed class NotificacoesApiFactory : FunerariaApiFactory, IAsyncLifetime
{
    public const string SegredoApp = "segredo-do-app-de-teste";

    public ValueTask InitializeAsync() => IniciarAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("WhatsApp:Habilitado", "true");
        builder.UseSetting("WhatsApp:PermitirFake", "true");
        builder.UseSetting("WhatsApp:Templates:lembrete_fatura", "lembrete_vencimento");
        builder.UseSetting("WhatsApp:Templates:notificacao", "aviso_geral");
        builder.UseSetting("WhatsApp:TiposNotificacao:0", "FUNERAL_STATUS");
        builder.UseSetting("WhatsApp:SegredoApp", SegredoApp);
        builder.UseSetting("WhatsApp:TokenVerificacao", "token-de-verificacao");
    }
}

[CollectionDefinition(Nome)]
public sealed class ApiCollection : ICollectionFixture<NotificacoesApiFactory>
{
    public const string Nome = "Api";
}

/// <summary>NOT-03: opt-in registrado (LGPD), envio só com consentimento, pela fila, e status de entrega pelo webhook.</summary>
[Collection(ApiCollection.Nome)]
public sealed class WhatsAppTests(NotificacoesApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = JsonApi.Opcoes;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private WhatsAppFake Fake => factory.Services.GetRequiredService<WhatsAppFake>();

    [Fact]
    public async Task Opt_in_exige_aceite_e_telefone_valido_e_guarda_data_e_ip()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (HttpClient cliente, Guid idUsuario) = Cliente(tenant, slug);

        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PutAsJsonAsync("/api/v1/notificacoes/whatsapp", new { telefone = "123", aceite = true }, Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PutAsJsonAsync("/api/v1/notificacoes/whatsapp", new { telefone = "(44) 99999-0000", aceite = false }, Json, Ct)).StatusCode);

        JsonObject aceito = await Cenarios.LerAsync(
            await cliente.PutAsJsonAsync("/api/v1/notificacoes/whatsapp", new { telefone = "(44) 99999-0000", aceite = true }, Json, Ct), HttpStatusCode.OK);

        Assert.True(aceito["ativo"]!.GetValue<bool>());
        Assert.Equal("(44) *****-0000", aceito["telefone"]!.GetValue<string>());
        (string telefone, DateTime? aceitoEm) = await ConsultarAsync<(string, DateTime?)>(
            "SELECT telefone, aceito_em FROM notificacoes.consentimentos_whatsapp WHERE id_usuario = @idUsuario", new { idUsuario });
        Assert.Equal("5544999990000", telefone);
        Assert.NotNull(aceitoEm);
    }

    [Fact]
    public async Task Lembrete_sai_so_para_quem_aceitou_e_para_de_sair_depois_da_revogacao()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (HttpClient cliente, Guid idUsuario) = Cliente(tenant, slug);
        Guid semConsentimento = Guid.NewGuid();
        await cliente.PutAsJsonAsync("/api/v1/notificacoes/whatsapp", new { telefone = "(44) 98888-1234", aceite = true }, Json, Ct);

        await EnfileirarLembreteAsync(tenant, idUsuario);
        await EnfileirarLembreteAsync(tenant, semConsentimento);

        await AguardarAsync(() => Fake.Enviadas.Any(e => e.Telefone == "5544988881234"), "lembrete enviado");
        var enviado = Fake.Enviadas.Single(e => e.Telefone == "5544988881234");
        Assert.Equal("lembrete_vencimento", enviado.Template);
        Assert.Equal(["Maria", "89,90", "10/12/2026", "https://pagar.stripe.test/fatura"], enviado.Parametros);
        Assert.Equal(0, await ConsultarAsync<long>("SELECT count(*) FROM notificacoes.mensagens_whatsapp WHERE id_usuario = @semConsentimento", new { semConsentimento }));

        Assert.Equal(HttpStatusCode.NoContent, (await cliente.DeleteAsync("/api/v1/notificacoes/whatsapp", Ct)).StatusCode);
        await EnfileirarLembreteAsync(tenant, idUsuario);
        Assert.Equal(1, await ConsultarAsync<long>("SELECT count(*) FROM notificacoes.mensagens_whatsapp WHERE id_usuario = @idUsuario", new { idUsuario }));
        Assert.False((await Cenarios.LerAsync(await cliente.GetAsync("/api/v1/notificacoes/whatsapp", Ct), HttpStatusCode.OK))["ativo"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Aviso_da_central_vai_pelo_whatsapp_so_nos_tipos_configurados()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (HttpClient cliente, Guid idUsuario) = Cliente(tenant, slug);
        await cliente.PutAsJsonAsync("/api/v1/notificacoes/whatsapp", new { telefone = "(11) 97777-4321", aceite = true }, Json, Ct);

        await NotificarAsync(tenant, idUsuario, "REAJUSTE_ANUAL", "Reajuste", "Fora da lista");
        await NotificarAsync(tenant, idUsuario, "FUNERAL_STATUS", "Funeral de José", "O velório começou.");

        await AguardarAsync(() => Fake.Enviadas.Any(e => e.Telefone == "5511977774321"), "aviso enviado");
        var enviado = Assert.Single(Fake.Enviadas, e => e.Telefone == "5511977774321");
        Assert.Equal("aviso_geral", enviado.Template);
        Assert.Equal(["Funeral de José", "O velório começou."], enviado.Parametros);
    }

    [Fact]
    public async Task Webhook_assinado_atualiza_o_status_de_entrega_e_sem_assinatura_e_recusado()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (HttpClient cliente, Guid idUsuario) = Cliente(tenant, slug);
        await cliente.PutAsJsonAsync("/api/v1/notificacoes/whatsapp", new { telefone = "(21) 96666-5555", aceite = true }, Json, Ct);
        await EnfileirarLembreteAsync(tenant, idUsuario);
        await AguardarAsync(() => Fake.Enviadas.Any(e => e.Telefone == "5521966665555"), "lembrete enviado");
        string idExterno = Fake.Enviadas.Single(e => e.Telefone == "5521966665555").IdExterno;
        await AguardarAsync(async () => await StatusAsync(idExterno) == "Enviada", "mensagem marcada como enviada");

        string entregue = Evento(idExterno, "delivered");
        Assert.Equal(HttpStatusCode.BadRequest, (await PostarAsync(entregue, "sha256=00")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostarAsync(Evento(idExterno, "read"), Assinar(Evento(idExterno, "read")))).StatusCode);
        // Fora de ordem: o "entregue" chega depois do "lido" e não volta o status.
        Assert.Equal(HttpStatusCode.OK, (await PostarAsync(entregue, Assinar(entregue))).StatusCode);

        Assert.Equal("Lida", await StatusAsync(idExterno));
    }

    [Fact]
    public async Task Verificacao_do_endereco_devolve_o_desafio_so_com_o_token_certo()
    {
        HttpClient http = factory.CreateClient();

        HttpResponseMessage certo = await http.GetAsync("/api/v1/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token=token-de-verificacao&hub.challenge=123", Ct);
        HttpResponseMessage errado = await http.GetAsync("/api/v1/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token=outro&hub.challenge=123", Ct);

        Assert.Equal("123", await certo.Content.ReadAsStringAsync(Ct));
        Assert.Equal(HttpStatusCode.Forbidden, errado.StatusCode);
    }

    private (HttpClient Http, Guid IdUsuario) Cliente(Guid tenant, string slug)
    {
        string token = TokenDeTeste.Gerar(Perfis.Cliente, tenant);
        Guid idUsuario = Guid.Parse(new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token).Subject);
        return (factory.Http(slug, token), idUsuario);
    }

    private async Task EnfileirarLembreteAsync(Guid tenant, Guid idUsuario)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);
        await scope.ServiceProvider.GetRequiredService<IEnvioWhatsApp>().EnfileirarAsync(
            new MensagemWhatsAppSolicitada(idUsuario, tenant, ModelosWhatsApp.LembreteFatura, ["Maria", "89,90", "10/12/2026", "https://pagar.stripe.test/fatura"]), Ct);
    }

    private async Task NotificarAsync(Guid tenant, Guid idUsuario, string tipo, string titulo, string mensagem)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);
        await scope.ServiceProvider.GetRequiredService<INotificacoes>().NotificarAsync(new NovaNotificacao([idUsuario], tenant, tipo, titulo, mensagem), Ct);
    }

    private Task<string?> StatusAsync(string idExterno) =>
        ConsultarAsync<string?>("SELECT status FROM notificacoes.mensagens_whatsapp WHERE id_externo = @idExterno", new { idExterno });

    private static string Evento(string idExterno, string status) => JsonSerializer.Serialize(new
    {
        @object = "whatsapp_business_account",
        entry = new[] { new { changes = new[] { new { value = new { statuses = new[] { new { id = idExterno, status } } } } } } }
    });

    private static string Assinar(string corpo) =>
        "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(NotificacoesApiFactory.SegredoApp), Encoding.UTF8.GetBytes(corpo)));

    private Task<HttpResponseMessage> PostarAsync(string corpo, string assinatura)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Hub-Signature-256", assinatura);
        return client.PostAsync("/api/v1/webhooks/whatsapp", new StringContent(corpo, Encoding.UTF8, "application/json"), Ct);
    }

    private async Task<T> ConsultarAsync<T>(string sql, object parametros)
    {
        await using DbConnection conexao = await factory.Services.GetRequiredService<IDbConnectionFactory>().OpenConnectionAsync(Ct);
        return await conexao.QuerySingleAsync<T>(new CommandDefinition(sql, parametros, cancellationToken: Ct));
    }

    private static Task AguardarAsync(Func<bool> condicao, string oQue) => AguardarAsync(() => Task.FromResult(condicao()), oQue);

    private static async Task AguardarAsync(Func<Task<bool>> condicao, string oQue)
    {
        for (int i = 0; i < 100; i++)
        {
            if (await condicao())
            {
                return;
            }

            await Task.Delay(200, Ct);
        }

        throw new TimeoutException($"Esperando: {oQue}.");
    }
}
