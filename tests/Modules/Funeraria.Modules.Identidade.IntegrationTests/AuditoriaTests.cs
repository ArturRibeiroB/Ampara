using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Funeraria.Modules.Identidade.Domain.Vinculos;
using Funeraria.Testing;
using static Funeraria.Modules.Identidade.IntegrationTests.SessaoDeTeste;

namespace Funeraria.Modules.Identidade.IntegrationTests;

/// <summary>AUT-09: quem fez o quê, visto pelo gestor (funerária) e pelo ADMIN (plataforma).</summary>
[Collection(ApiCollection.Nome)]
public sealed class AuditoriaTests(IdentidadeApiFactory factory)
{
    [Fact]
    public async Task Gestor_ve_quem_alterou_o_acesso_com_valores_antes_e_depois()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (Guid idGestor, string emailGestor) = await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Funeraria));
        (Guid idFuncionario, _) = await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Funcionario));
        (HttpClient gestor, _, _) = await factory.EntrarAsync(slug, emailGestor);

        await gestor.PatchAsJsonAsync($"/api/v1/gestao/usuarios/{idFuncionario}/perfil", new { perfil = "CONSULTOR" }, Json, Ct);

        JsonObject pagina = (await gestor.GetFromJsonAsync<JsonObject>("/api/v1/gestao/auditoria?acao=usuario_perfil_alterado", Json, Ct))!;
        JsonNode registro = Assert.Single(pagina["itens"]!.AsArray())!;
        Assert.Equal("Usuario", registro["entidade"]!.GetValue<string>());
        Assert.Equal(idFuncionario, registro["id_entidade"]!.GetValue<Guid>());
        Assert.Equal(idGestor, registro["id_usuario"]!.GetValue<Guid>());
        Assert.Equal("Usuário de Teste", registro["nome_usuario"]!.GetValue<string>());
        Assert.Equal("Funcionario", registro["valores_anteriores"]!["perfil"]!.GetValue<string>());
        Assert.Equal("Consultor", registro["valores_novos"]!["perfil"]!.GetValue<string>());
    }

    [Fact]
    public async Task Cada_funeraria_ve_so_a_propria_auditoria_e_funcionario_nao_consulta()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (Guid outra, string slugOutra) = await factory.CriarFunerariaAsync();
        (_, string emailGestor) = await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Funeraria));
        (Guid idFuncionario, string emailFuncionario) = await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Funcionario));
        (_, string emailGestorOutra) = await factory.CriarUsuarioAsync((outra, PerfilVinculo.Funeraria));
        (HttpClient gestor, _, _) = await factory.EntrarAsync(slug, emailGestor);
        await gestor.PatchAsJsonAsync($"/api/v1/gestao/usuarios/{idFuncionario}/perfil", new { perfil = "CONSULTOR" }, Json, Ct);

        (HttpClient gestorOutra, _, _) = await factory.EntrarAsync(slugOutra, emailGestorOutra);
        JsonObject daOutra = (await gestorOutra.GetFromJsonAsync<JsonObject>($"/api/v1/gestao/auditoria?id_entidade={idFuncionario}", Json, Ct))!;
        Assert.Equal(0, daOutra["total"]!.GetValue<int>());

        (HttpClient consultor, _, _) = await factory.EntrarAsync(slug, emailFuncionario);
        Assert.Equal(HttpStatusCode.Forbidden, (await consultor.GetAsync("/api/v1/gestao/auditoria", Ct)).StatusCode);
    }

    [Fact]
    public async Task Acao_do_admin_aparece_na_plataforma_e_na_funeraria_afetada()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync(Tenants.Domain.Funerarias.StatusFuneraria.Pendente);
        (_, string emailGestor) = await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Funeraria));
        HttpResponseMessage login = await factory.LoginAsync(slug: null, FunerariaApiFactory.EmailAdmin, FunerariaApiFactory.SenhaPadrao);
        string tokenAdmin = (await login.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["access_token"]!.GetValue<string>();
        HttpClient admin = factory.Cliente(slug: null, tokenAdmin);

        Assert.Equal(HttpStatusCode.NoContent,
            (await admin.PatchAsJsonAsync($"/api/v1/admin/tenants/{tenant}/status", new { status = "ATIVA" }, Json, Ct)).StatusCode);

        JsonObject plataforma = (await admin.GetFromJsonAsync<JsonObject>($"/api/v1/admin/auditoria?id_entidade={tenant}", Json, Ct))!;
        JsonNode registro = Assert.Single(plataforma["itens"]!.AsArray())!;
        Assert.Equal("FUNERARIA_STATUS_ALTERADO", registro["acao"]!.GetValue<string>());
        Assert.Equal("Pendente", registro["valores_anteriores"]!["status"]!.GetValue<string>());

        // A funerária vê o que o ADMIN fez nela (acesso de suporte fica registrado e visível).
        (HttpClient gestor, _, _) = await factory.EntrarAsync(slug, emailGestor);
        JsonObject daFuneraria = (await gestor.GetFromJsonAsync<JsonObject>("/api/v1/gestao/auditoria?acao=FUNERARIA_STATUS_ALTERADO", Json, Ct))!;
        Assert.Equal(1, daFuneraria["total"]!.GetValue<int>());
    }
}
