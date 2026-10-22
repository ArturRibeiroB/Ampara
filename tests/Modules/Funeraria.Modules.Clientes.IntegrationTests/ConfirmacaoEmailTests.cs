using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Funeraria.Modules.Notificacoes.Application.Email;
using Funeraria.Testing;
using static Funeraria.Modules.Clientes.IntegrationTests.ApiCliente;

namespace Funeraria.Modules.Clientes.IntegrationTests;

/// <summary>Confirmação de e-mail depois do cadastro do cliente (AUT-05).</summary>
[Collection(ApiCollection.Nome)]
public sealed class ConfirmacaoEmailTests(ClientesApiFactory factory)
{
    [Fact]
    public async Task Cadastro_envia_link_de_confirmacao_que_vale_uma_vez()
    {
        (_, string slug) = await factory.CriarFunerariaAsync();
        Dictionary<string, object?> corpo = NovoCliente();
        string email = (string)corpo["email"]!;
        await factory.CadastrarAsync(slug, corpo);

        EmailMensagem mensagem = await factory.Emails.AguardarAsync(email);
        Assert.Contains($"http://{slug}.teste.localhost/confirmar-email?token=", mensagem.CorpoTexto, StringComparison.Ordinal);
        string token = EmailsCapturados.TokenDoLink(mensagem);

        HttpResponseMessage confirmado = await ConfirmarAsync(slug, token);
        Assert.Equal(HttpStatusCode.NoContent, confirmado.StatusCode);

        JsonObject reuso = await (await ConfirmarAsync(slug, token)).ProblemAsync(HttpStatusCode.BadRequest);
        Assert.Equal("Auth.LinkConfirmacaoInvalido", reuso["code"]!.GetValue<string>());
        Assert.Equal("Link de confirmação inválido ou expirado.", reuso["detail"]!.GetValue<string>());
    }

    [Fact]
    public async Task Conta_com_email_confirmado_recebe_boas_vindas_ao_se_cadastrar_em_outra_funeraria()
    {
        (_, string primeira) = await factory.CriarFunerariaAsync();
        (_, string segunda) = await factory.CriarFunerariaAsync();
        Dictionary<string, object?> corpo = NovoCliente();
        string email = (string)corpo["email"]!;
        await factory.CadastrarAsync(primeira, corpo);
        string token = EmailsCapturados.TokenDoLink(await factory.Emails.AguardarAsync(email));
        Assert.Equal(HttpStatusCode.NoContent, (await ConfirmarAsync(primeira, token)).StatusCode);

        await factory.CadastrarAsync(segunda, NovoCliente(email: email));

        EmailMensagem boasVindas = await factory.Emails.AguardarAsync(
            email, e => e.CorpoTexto.Contains($"http://{segunda}.teste.localhost/login", StringComparison.Ordinal));
        Assert.DoesNotContain("confirmar-email", boasVindas.CorpoTexto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Token_invalido_retorna_400()
    {
        (_, string slug) = await factory.CriarFunerariaAsync();

        JsonObject problem = await (await ConfirmarAsync(slug, "nao-existe")).ProblemAsync(HttpStatusCode.BadRequest);

        Assert.Equal("Auth.LinkConfirmacaoInvalido", problem["code"]!.GetValue<string>());
    }

    private Task<HttpResponseMessage> ConfirmarAsync(string slug, string token) =>
        factory.Cliente(slug).PostAsJsonAsync("/api/v1/auth/confirmar-email", new { token }, Json, Ct);
}
