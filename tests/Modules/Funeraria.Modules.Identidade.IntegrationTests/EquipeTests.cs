using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.Modules.Identidade.Domain.Vinculos;
using Funeraria.Modules.Notificacoes.Application.Email;
using Funeraria.Testing;
using static Funeraria.Modules.Identidade.IntegrationTests.SessaoDeTeste;

namespace Funeraria.Modules.Identidade.IntegrationTests;

/// <summary>AUT-07 (cadastro de funcionário e consultor) e AUT-08 (gestão da equipe), com o perfil CONSULTOR do DEC-11.</summary>
[Collection(ApiCollection.Nome)]
public sealed class EquipeTests(IdentidadeApiFactory factory)
{
    [Fact]
    public async Task Gestor_cadastra_funcionario_que_define_a_senha_pelo_convite_e_entra()
    {
        (HttpClient gestor, string slug, _) = await GestorAsync();
        string email = NovoEmail();

        HttpResponseMessage response = await gestor.PostAsJsonAsync("/api/v1/gestao/funcionarios", NovoFuncionario(email, cargo: "Atendente"), Json, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        JsonObject membro = (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
        Assert.Equal("FUNCIONARIO", membro["perfil"]!.GetValue<string>());
        Assert.Equal("Atendente", membro["cargo"]!.GetValue<string>());
        Assert.True(membro["ativo"]!.GetValue<bool>());

        EmailMensagem convite = await factory.Emails.AguardarAsync(email, e => e.CorpoTexto.Contains("definir-senha", StringComparison.Ordinal));
        Assert.Contains("funcionário(a)", convite.CorpoTexto, StringComparison.Ordinal);
        HttpResponseMessage definida = await factory.Cliente(slug).PostAsJsonAsync("/api/v1/auth/definir-senha",
            new { token = EmailsCapturados.TokenDoLink(convite), senha = FunerariaApiFactory.SenhaPadrao }, Json, Ct);
        Assert.Equal(HttpStatusCode.NoContent, definida.StatusCode);

        HttpResponseMessage login = await factory.LoginAsync(slug, email, FunerariaApiFactory.SenhaPadrao);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(Perfis.Funcionario, await PerfilDoTokenAsync(login));
    }

    [Fact]
    public async Task Consultor_atende_clientes_mas_nao_acessa_a_gestao_da_funeraria()
    {
        (HttpClient gestor, string slug, Guid tenant) = await GestorAsync();
        (_, string email) = await factory.CriarUsuarioAsync();

        HttpResponseMessage criado = await gestor.PostAsJsonAsync("/api/v1/gestao/funcionarios", NovoFuncionario(email, perfil: "CONSULTOR"), Json, Ct);
        Assert.Equal(HttpStatusCode.Created, criado.StatusCode);

        // A conta já existia: entra com a senha que já usa e recebe só o aviso de acesso (DEC-02).
        await factory.Emails.AguardarAsync(email, e => e.Assunto.Contains("Acesso liberado", StringComparison.Ordinal));
        HttpResponseMessage login = await factory.LoginAsync(slug, email, FunerariaApiFactory.SenhaPadrao);
        Assert.Equal(Perfis.Consultor, await PerfilDoTokenAsync(login));
        (HttpClient consultor, _, _) = await factory.EntrarAsync(slug, email);

        Assert.Equal(HttpStatusCode.OK, (await consultor.GetAsync("/api/v1/gestao/clientes", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await consultor.GetAsync("/api/v1/gestao/contratos", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await consultor.GetAsync("/api/v1/gestao/configuracoes", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await consultor.GetAsync("/api/v1/gestao/usuarios", Ct)).StatusCode);
    }

    [Fact]
    public async Task Mesmo_usuario_ou_mesmo_cpf_duas_vezes_e_recusado()
    {
        (HttpClient gestor, _, _) = await GestorAsync();
        string email = NovoEmail();
        string cpf = DocumentosDeTeste.NovoCpf();
        Assert.Equal(HttpStatusCode.Created, (await gestor.PostAsJsonAsync("/api/v1/gestao/funcionarios", NovoFuncionario(email, cpf: cpf), Json, Ct)).StatusCode);

        JsonObject mesmoUsuario = await (await gestor.PostAsJsonAsync("/api/v1/gestao/funcionarios", NovoFuncionario(email), Json, Ct))
            .ProblemaAsync(HttpStatusCode.Conflict);
        JsonObject mesmoCpf = await (await gestor.PostAsJsonAsync("/api/v1/gestao/funcionarios", NovoFuncionario(NovoEmail(), cpf: cpf), Json, Ct))
            .ProblemaAsync(HttpStatusCode.Conflict);

        Assert.Equal("Este usuário já é funcionário desta funerária.", mesmoUsuario["detail"]!.GetValue<string>());
        Assert.Equal("CPF já cadastrado nesta funerária.", mesmoCpf["detail"]!.GetValue<string>());
    }

    [Fact]
    public async Task Email_de_cliente_da_funeraria_nao_vira_equipe()
    {
        (HttpClient gestor, _, Guid tenant) = await GestorAsync();
        (_, string email) = await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Cliente));

        JsonObject problem = await (await gestor.PostAsJsonAsync("/api/v1/gestao/funcionarios", NovoFuncionario(email), Json, Ct))
            .ProblemaAsync(HttpStatusCode.Conflict);

        Assert.Equal("Equipe.EmailDeCliente", problem["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Funcionario_nao_cadastra_funcionario_e_recebe_a_mensagem_do_card()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (_, string email) = await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Funcionario));
        (HttpClient funcionario, _, _) = await factory.EntrarAsync(slug, email);

        JsonObject problem = await (await funcionario.PostAsJsonAsync("/api/v1/gestao/funcionarios", NovoFuncionario(NovoEmail()), Json, Ct))
            .ProblemaAsync(HttpStatusCode.Forbidden);

        Assert.Equal("Acesso negado. Apenas o gestor da funerária pode cadastrar funcionários.", problem["detail"]!.GetValue<string>());
    }

    [Fact]
    public async Task Dados_invalidos_retornam_400_com_as_mensagens()
    {
        (HttpClient gestor, _, _) = await GestorAsync();

        JsonObject problem = await (await gestor.PostAsJsonAsync("/api/v1/gestao/funcionarios",
            new { nome = "", cpf = "123", email = "x@", perfil = "FUNERARIA" }, Json, Ct)).ProblemaAsync(HttpStatusCode.BadRequest);

        JsonObject erros = problem["errors"]!.AsObject();
        Assert.Equal("Nome é obrigatório.", erros["nome"]![0]!.GetValue<string>());
        Assert.Equal("CPF não informado ou em formato inválido.", erros["cpf"]![0]!.GetValue<string>());
        Assert.Equal("E-mail inválido.", erros["email"]![0]!.GetValue<string>());
        Assert.Equal("Perfil inválido. Use FUNCIONARIO ou CONSULTOR.", erros["perfil"]![0]!.GetValue<string>());
    }

    [Fact]
    public async Task Lista_a_equipe_com_filtros_sem_mostrar_clientes()
    {
        (HttpClient gestor, _, Guid tenant) = await GestorAsync();
        await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Cliente));
        await gestor.PostAsJsonAsync("/api/v1/gestao/funcionarios", NovoFuncionario(NovoEmail(), nome: "Ana Consultora", perfil: "CONSULTOR"), Json, Ct);
        await gestor.PostAsJsonAsync("/api/v1/gestao/funcionarios", NovoFuncionario(NovoEmail(), nome: "Bruno Atendente"), Json, Ct);

        JsonObject todos = (await gestor.GetFromJsonAsync<JsonObject>("/api/v1/gestao/usuarios", Json, Ct))!;
        Assert.Equal(3, todos["total"]!.GetValue<int>());
        Assert.DoesNotContain(todos["itens"]!.AsArray(), m => m!["perfil"]!.GetValue<string>() == "CLIENTE");

        JsonObject consultores = (await gestor.GetFromJsonAsync<JsonObject>("/api/v1/gestao/usuarios?perfil=CONSULTOR", Json, Ct))!;
        Assert.Equal("Ana Consultora", Assert.Single(consultores["itens"]!.AsArray())!["nome"]!.GetValue<string>());

        JsonObject busca = (await gestor.GetFromJsonAsync<JsonObject>("/api/v1/gestao/usuarios?busca=bruno", Json, Ct))!;
        Assert.Equal("Bruno Atendente", Assert.Single(busca["itens"]!.AsArray())!["nome"]!.GetValue<string>());

        Assert.Equal(HttpStatusCode.BadRequest, (await gestor.GetAsync("/api/v1/gestao/usuarios?status=SUMIDO", Ct)).StatusCode);
    }

    [Fact]
    public async Task Desativar_derruba_so_as_sessoes_desta_funeraria()
    {
        (HttpClient gestor, string slug, Guid tenant) = await GestorAsync();
        (Guid outra, string slugOutra) = await factory.CriarFunerariaAsync();
        (Guid idUsuario, string email) = await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Funcionario), (outra, PerfilVinculo.Funcionario));
        (_, _, string refreshAqui) = await factory.EntrarAsync(slug, email);
        (_, _, string refreshOutra) = await factory.EntrarAsync(slugOutra, email);

        HttpResponseMessage response = await gestor.PatchAsJsonAsync($"/api/v1/gestao/usuarios/{idUsuario}/status", new { ativo = false }, Json, Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.RefreshAsync(slug, refreshAqui)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.RefreshAsync(slugOutra, refreshOutra)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await factory.LoginAsync(slug, email, FunerariaApiFactory.SenhaPadrao)).StatusCode);

        JsonObject inativos = (await gestor.GetFromJsonAsync<JsonObject>("/api/v1/gestao/usuarios?status=INATIVO", Json, Ct))!;
        Assert.Equal(idUsuario, Assert.Single(inativos["itens"]!.AsArray())!["id_usuario"]!.GetValue<Guid>());

        // Reativar devolve o acesso.
        await gestor.PatchAsJsonAsync($"/api/v1/gestao/usuarios/{idUsuario}/status", new { ativo = true }, Json, Ct);
        Assert.Equal(HttpStatusCode.OK, (await factory.LoginAsync(slug, email, FunerariaApiFactory.SenhaPadrao)).StatusCode);
    }

    [Fact]
    public async Task Gestor_nao_desativa_a_si_mesmo_nem_rebaixa_o_ultimo_gestor()
    {
        (HttpClient gestor, _, Guid tenant) = await GestorAsync();
        Guid euMesmo = await IdDoGestorAsync(gestor);

        JsonObject proprio = await (await gestor.PatchAsJsonAsync($"/api/v1/gestao/usuarios/{euMesmo}/status", new { ativo = false }, Json, Ct))
            .ProblemaAsync(HttpStatusCode.Conflict);
        JsonObject ultimo = await (await gestor.PatchAsJsonAsync($"/api/v1/gestao/usuarios/{euMesmo}/perfil", new { perfil = "FUNCIONARIO" }, Json, Ct))
            .ProblemaAsync(HttpStatusCode.Conflict);

        Assert.Equal("Você não pode desativar o seu próprio acesso.", proprio["detail"]!.GetValue<string>());
        Assert.Equal("A funerária precisa de ao menos um gestor ativo.", ultimo["detail"]!.GetValue<string>());

        // Com outro gestor ativo, pode passar a gestão.
        await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Funeraria));
        Assert.Equal(HttpStatusCode.NoContent,
            (await gestor.PatchAsJsonAsync($"/api/v1/gestao/usuarios/{euMesmo}/perfil", new { perfil = "FUNCIONARIO" }, Json, Ct)).StatusCode);
    }

    [Fact]
    public async Task Novo_perfil_vale_na_renovacao_da_sessao()
    {
        (HttpClient gestor, string slug, Guid tenant) = await GestorAsync();
        (Guid idUsuario, string email) = await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Funcionario));
        (_, _, string refresh) = await factory.EntrarAsync(slug, email);

        Assert.Equal(HttpStatusCode.NoContent,
            (await gestor.PatchAsJsonAsync($"/api/v1/gestao/usuarios/{idUsuario}/perfil", new { perfil = "CONSULTOR" }, Json, Ct)).StatusCode);

        HttpResponseMessage renovada = await factory.RefreshAsync(slug, refresh);
        Assert.Equal(Perfis.Consultor, await PerfilDoTokenAsync(renovada));
    }

    [Fact]
    public async Task Cliente_ou_usuario_de_outra_funeraria_nao_e_encontrado()
    {
        (HttpClient gestor, _, Guid tenant) = await GestorAsync();
        (Guid outra, _) = await factory.CriarFunerariaAsync();
        (Guid cliente, _) = await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Cliente));
        (Guid deOutra, _) = await factory.CriarUsuarioAsync((outra, PerfilVinculo.Funcionario));

        foreach (Guid id in new[] { cliente, deOutra })
        {
            HttpResponseMessage response = await gestor.PatchAsJsonAsync($"/api/v1/gestao/usuarios/{id}/status", new { ativo = false }, Json, Ct);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    private async Task<(HttpClient Gestor, string Slug, Guid Tenant)> GestorAsync()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (_, string email) = await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Funeraria));
        (HttpClient http, _, _) = await factory.EntrarAsync(slug, email);
        return (http, slug, tenant);
    }

    private static async Task<Guid> IdDoGestorAsync(HttpClient gestor)
    {
        JsonObject lista = (await gestor.GetFromJsonAsync<JsonObject>("/api/v1/gestao/usuarios?perfil=FUNERARIA", Json, Ct))!;
        return Assert.Single(lista["itens"]!.AsArray())!["id_usuario"]!.GetValue<Guid>();
    }

    private static string NovoEmail() => $"equipe-{Guid.NewGuid():N}@teste.localhost";

    private static object NovoFuncionario(
        string email, string? cpf = null, string nome = "Maria Souza", string? cargo = null, string perfil = "FUNCIONARIO") => new
    {
        nome,
        cpf = cpf ?? DocumentosDeTeste.NovoCpf(),
        email,
        cargo,
        telefone = "(44) 99999-0000",
        perfil
    };
}
