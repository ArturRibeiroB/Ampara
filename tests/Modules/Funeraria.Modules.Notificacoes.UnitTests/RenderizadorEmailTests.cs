using Funeraria.Modules.Notificacoes.Application.Email;
using Funeraria.Modules.Notificacoes.Contracts;
using Funeraria.Modules.Tenants.Contracts;

namespace Funeraria.Modules.Notificacoes.UnitTests;

public sealed class RenderizadorEmailTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly RenderizadorEmail _renderizador = new(new TenantsFalso(
        new MarcaFunerariaDto(Tenant, "paz", "Funerária Paz", "https://cdn.exemplo/logo.png", "#112233")));

    public static TheoryData<string> Modelos() => new(RenderizadorEmail.Modelos);

    [Theory]
    [MemberData(nameof(Modelos))]
    public async Task Todo_modelo_renderiza_html_e_texto_sem_campo_sobrando(string modelo)
    {
        EmailMensagem email = await _renderizador.RenderizarAsync(Solicitar(modelo), Ct);

        Assert.DoesNotContain("{{", email.CorpoHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", email.CorpoTexto, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", email.Assunto, StringComparison.Ordinal);
        Assert.Contains("https://exemplo/link", email.CorpoTexto, StringComparison.Ordinal);
        Assert.Contains("Maria", email.CorpoHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Email_leva_nome_logo_e_cor_da_funeraria()
    {
        EmailMensagem email = await _renderizador.RenderizarAsync(Solicitar(ModelosEmail.Convite), Ct);

        Assert.Equal("Convite para acessar Funerária Paz", email.Assunto);
        Assert.Contains("""<img src="https://cdn.exemplo/logo.png" alt="Funerária Paz" """, email.CorpoHtml, StringComparison.Ordinal);
        Assert.Contains("background:#112233", email.CorpoHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Email_da_plataforma_usa_o_nome_da_plataforma()
    {
        EmailMensagem email = await _renderizador.RenderizarAsync(Solicitar(ModelosEmail.RecuperacaoSenha) with { IdTenant = null }, Ct);

        Assert.Contains("Funerária Online", email.CorpoHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dados_do_usuario_sao_escapados_e_nao_viram_campo_do_modelo()
    {
        EmailSolicitado solicitado = Solicitar(ModelosEmail.Convite, nome: "<script>alert(1)</script> {{link}} {{{conteudo}}}");

        EmailMensagem email = await _renderizador.RenderizarAsync(solicitado, Ct);

        Assert.DoesNotContain("<script>", email.CorpoHtml, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", email.CorpoHtml, StringComparison.Ordinal);
        Assert.Contains("{{link}} {{{conteudo}}}", email.CorpoHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Campo_que_falta_falha_em_vez_de_sair_no_email()
    {
        var incompleto = new EmailSolicitado(ModelosEmail.Convite, "maria@exemplo.com", "Maria", Tenant,
            new Dictionary<string, string> { ["nome"] = "Maria" });

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => _renderizador.RenderizarAsync(incompleto, Ct));
        Assert.Contains("'papel'", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Modelo_desconhecido_falha() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => _renderizador.RenderizarAsync(Solicitar("inexistente"), Ct));

    private static EmailSolicitado Solicitar(string modelo, string nome = "Maria") => new(
        modelo, "maria@exemplo.com", nome, Tenant,
        new Dictionary<string, string>
        {
            ["nome"] = nome,
            ["papel"] = "gestor(a)",
            ["link"] = "https://exemplo/link",
            ["horas"] = "72",
            ["minutos"] = "30"
        });

    private sealed class TenantsFalso(MarcaFunerariaDto marca) : ITenantsApi
    {
        public Task<MarcaFunerariaDto?> ObterMarcaAsync(Guid idTenant, CancellationToken cancellationToken = default) =>
            Task.FromResult<MarcaFunerariaDto?>(idTenant == marca.IdTenant ? marca : null);

        public Task<ParametrosCobrancaDto?> ObterParametrosCobrancaAsync(Guid idTenant, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TimeSpan?> ObterPrazoAceiteTerceiroAsync(Guid idTenant, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<FunerariaResumoDto?> ObterPorSlugAsync(string slug, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<FunerariaResumoDto?> ObterPorIdAsync(Guid idTenant, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<FunerariaResumoDto>> ListarAsync(IReadOnlyCollection<Guid> idsTenant, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
