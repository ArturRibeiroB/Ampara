using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Infrastructure.Tenancy;
using Funeraria.Modules.Financeiro.Application.Abstractions;
using Funeraria.Modules.Financeiro.Application.Cobrancas;
using Funeraria.Modules.Financeiro.Domain.Cobranca;
using Funeraria.Modules.Financeiro.Infrastructure.Stripe;
using Funeraria.Modules.Relatorios.Application;
using Funeraria.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Relatorios.IntegrationTests;

public sealed class RelatoriosApiFactory : FunerariaApiFactory, IAsyncLifetime
{
    public ValueTask InitializeAsync() => IniciarAsync();
}

[CollectionDefinition(Nome)]
public sealed class ApiCollection : ICollectionFixture<RelatoriosApiFactory>
{
    public const string Nome = "Api";
}

/// <summary>REL-01 a REL-03: indicadores, isolamento entre funerárias, exportação em CSV e painel do ADMIN.</summary>
[Collection(ApiCollection.Nome)]
public sealed class RelatoriosTests(RelatoriosApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = JsonApi.Opcoes;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private GatewayFake Fake => factory.Services.GetRequiredService<GatewayFake>();

    [Fact]
    public async Task Dashboard_conta_so_a_funeraria_e_fica_um_minuto_em_cache()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        FunerariaDeTeste outra = await factory.CriarFunerariaContratavelAsync();
        await ContratoAtivoAsync(outra);
        string idConta = await CadastrarContaAsync(funeraria);
        HttpClient equipe = funeraria.Equipe(Perfis.Funcionario);

        (Guid emDia, decimal valorEmDia) = await ContratoComFaturaAsync(funeraria);
        (Guid emAtraso, _) = await ContratoComFaturaAsync(funeraria);
        await factory.AtivarContratoAsync(emDia);
        await factory.AtivarContratoAsync(emAtraso);

        // Um paga; o outro atrasa desde ontem.
        Fatura paga = (await FaturasAsync(funeraria.Tenant, emDia)).Single();
        Fake.Pagar(paga.IdStripeInvoice, DateTime.UtcNow);
        await WebhookAsync(TiposEventoStripe.FaturaPaga, idConta, paga.IdStripeInvoice);
        await AguardarAsync(async () => (await FaturasAsync(funeraria.Tenant, emDia)).Single().Status == StatusFatura.Paga, "fatura paga");
        Fatura atrasada = (await FaturasAsync(funeraria.Tenant, emAtraso)).Single();
        DateOnly hoje = DateOnly.FromDateTime(DateTime.Now);
        Fake.Vencer(atrasada.IdStripeInvoice, hoje.AddDays(-1));
        await WebhookAsync(TiposEventoStripe.FaturaAtualizada, idConta, atrasada.IdStripeInvoice);
        await AguardarAsync(async () => (await FaturasAsync(funeraria.Tenant, emAtraso)).Single().DataVencimento < hoje, "vencimento atualizado");
        await InadimplenciaAsync(funeraria.Tenant);

        await FuneralAsync(funeraria, emDia);

        JsonObject dashboard = await Cenarios.LerAsync(await equipe.GetAsync("/api/v1/gestao/dashboard", Ct), HttpStatusCode.OK);
        Assert.Equal(2, dashboard["contratos_ativos"]!.GetValue<long>());
        Assert.Equal(2, dashboard["novos_contratos_no_mes"]!.GetValue<long>());
        Assert.Equal(50m, dashboard["taxa_inadimplencia"]!.GetValue<decimal>());

        // O primeiro vencimento é daqui a alguns dias e pode cair no mês seguinte.
        Fatura[] doPlano = [.. await FaturasAsync(funeraria.Tenant, emDia), .. await FaturasAsync(funeraria.Tenant, emAtraso)];
        decimal esperada = doPlano.Where(f => f.DataVencimento.Year == hoje.Year && f.DataVencimento.Month == hoje.Month).Sum(f => f.Valor);
        Assert.Equal(esperada, dashboard["receita_prevista_no_mes"]!.GetValue<decimal>());
        Assert.Equal(valorEmDia, dashboard["receita_recebida_no_mes"]!.GetValue<decimal>());
        Assert.Equal(1, dashboard["funerais_em_andamento"]!.GetValue<long>());
        Assert.Equal(0, dashboard["translados_em_andamento"]!.GetValue<long>());

        // Um contrato novo só aparece depois de vencer o cache.
        await ContratoComFaturaAsync(funeraria);
        JsonObject emCache = await Cenarios.LerAsync(await equipe.GetAsync("/api/v1/gestao/dashboard", Ct), HttpStatusCode.OK);
        Assert.Equal(2, emCache["novos_contratos_no_mes"]!.GetValue<long>());

        Assert.Equal(HttpStatusCode.Forbidden, (await (await funeraria.CriarClienteAsync()).Http.GetAsync("/api/v1/gestao/dashboard", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.Http(funeraria.Slug).GetAsync("/api/v1/gestao/dashboard", Ct)).StatusCode);
    }

    [Fact]
    public async Task Relatorios_filtram_exportam_csv_e_sao_so_do_gestor()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        FunerariaDeTeste outra = await factory.CriarFunerariaContratavelAsync();
        await ContratoAtivoAsync(outra);
        await CadastrarContaAsync(funeraria);
        (Guid idContrato, decimal valor) = await ContratoComFaturaAsync(funeraria);
        await FuneralAsync(funeraria, idContrato);
        HttpClient gestor = funeraria.Equipe();
        DateOnly hoje = DateOnly.FromDateTime(DateTime.Now);
        string periodo = $"de={hoje.AddDays(-400 + 40):yyyy-MM-dd}&ate={hoje.AddDays(5):yyyy-MM-dd}";

        foreach (string rota in new[] { "financeiro", "contratos", "funerais" })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await funeraria.Equipe(Perfis.Funcionario).GetAsync($"/api/v1/gestao/relatorios/{rota}", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await gestor.GetAsync($"/api/v1/gestao/relatorios/{rota}?de=2026-02-01&ate=2026-01-01", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await gestor.GetAsync($"/api/v1/gestao/relatorios/{rota}?de=2024-01-01&ate=2026-01-01", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await gestor.GetAsync($"/api/v1/gestao/relatorios/{rota}?formato=pdf", Ct)).StatusCode);
            Assert.Single((await gestor.GetFromJsonAsync<JsonArray>($"/api/v1/gestao/relatorios/{rota}?{periodo}", Json, Ct))!);
        }

        // Status como na API, com ou sem sublinhado.
        Assert.Single((await gestor.GetFromJsonAsync<JsonArray>($"/api/v1/gestao/relatorios/financeiro?{periodo}&status=PENDENTE", Json, Ct))!);
        Assert.Empty((await gestor.GetFromJsonAsync<JsonArray>($"/api/v1/gestao/relatorios/financeiro?{periodo}&status=PAGA", Json, Ct))!);
        Assert.Single((await gestor.GetFromJsonAsync<JsonArray>($"/api/v1/gestao/relatorios/contratos?{periodo}&status=ATIVO", Json, Ct))!);
        Assert.Empty((await gestor.GetFromJsonAsync<JsonArray>($"/api/v1/gestao/relatorios/contratos?{periodo}&status=A_PAGAR", Json, Ct))!);

        HttpResponseMessage csv = await gestor.GetAsync($"/api/v1/gestao/relatorios/financeiro?{periodo}&formato=csv", Ct);
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.Equal("text/csv", csv.Content.Headers.ContentType!.MediaType);
        Assert.Equal("faturas.csv", csv.Content.Headers.ContentDisposition!.FileName);
        byte[] bytes = await csv.Content.ReadAsByteArrayAsync(Ct);
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        string[] linhas = Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("Fatura;Contrato;Competência;Valor", linhas[0], StringComparison.Ordinal);
        Assert.Contains(idContrato.ToString(), Assert.Single(linhas[1..]), StringComparison.Ordinal);
        Assert.Contains(valor.ToString("0.00", new System.Globalization.CultureInfo("pt-BR")), linhas[1], StringComparison.Ordinal);

        await ContratoComFaturaAsync(funeraria);
        Assert.Single((await gestor.GetFromJsonAsync<JsonArray>($"/api/v1/gestao/relatorios/contratos?{periodo}&status=A_PAGAR", Json, Ct))!);
    }

    [Fact]
    public void Csv_neutraliza_formulas_e_escapa_separadores()
    {
        ColunaCsv<string>[] colunas = [new("Nome", s => s)];

        byte[] csv = CsvRelatorio.Gerar(colunas, ["=HYPERLINK(\"x\")", "Silva; Souza", "@soma", "Ana"]);

        string[] linhas = Encoding.UTF8.GetString(csv[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(["Nome", "\"'=HYPERLINK(\"\"x\"\")\"", "\"Silva; Souza\"", "'@soma", "Ana"], linhas);
    }

    [Fact]
    public async Task Painel_da_plataforma_agrega_sem_dados_pessoais_e_e_so_do_admin()
    {
        FunerariaDeTeste comConta = await factory.CriarFunerariaContratavelAsync();
        FunerariaDeTeste semConta = await factory.CriarFunerariaContratavelAsync();
        string idConta = await CadastrarContaAsync(comConta);
        (Guid idContrato, decimal valor) = await ContratoComFaturaAsync(comConta);
        ClienteDeTeste titular = await comConta.CriarClienteAsync();
        Fatura fatura = (await FaturasAsync(comConta.Tenant, idContrato)).Single();
        Fake.Pagar(fatura.IdStripeInvoice, DateTime.UtcNow);
        await WebhookAsync(TiposEventoStripe.FaturaPaga, idConta, fatura.IdStripeInvoice);
        await AguardarAsync(async () => (await FaturasAsync(comConta.Tenant, idContrato)).Single().Status == StatusFatura.Paga, "fatura paga");

        using HttpClient admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenDeTeste.Gerar(Perfis.Admin, null));
        JsonObject painel = await Cenarios.LerAsync(await admin.GetAsync("/api/v1/admin/dashboard", Ct), HttpStatusCode.OK);

        JsonNode pronta = painel["funerarias"]!.AsArray().Single(f => f!["id_tenant"]!.GetValue<Guid>() == comConta.Tenant)!;
        Assert.Equal(valor, pronta["volume_pago_no_mes"]!.GetValue<decimal>());
        Assert.False(pronta["pendencia_stripe"]!.GetValue<bool>());
        Assert.True(painel["funerarias"]!.AsArray().Single(f => f!["id_tenant"]!.GetValue<Guid>() == semConta.Tenant)!["pendencia_stripe"]!.GetValue<bool>());
        Assert.True(painel["funerarias_com_pendencia_stripe"]!.GetValue<long>() >= 1);
        Assert.True(painel["volume_pago_no_mes"]!.GetValue<decimal>() >= valor);
        Assert.Contains(painel["funerarias_por_status"]!.AsArray(), s => s!["quantidade"]!.GetValue<long>() >= 2);

        // A taxa vem da configuração (DEC-09) sobre o volume pelo Stripe.
        decimal volumeTaxavel = painel["funerarias"]!.AsArray().Sum(f => f!["volume_taxavel_no_mes"]!.GetValue<decimal>());
        Assert.Equal(decimal.Round(volumeTaxavel * 0.02m, 2), painel["receita_plataforma_no_mes"]!.GetValue<decimal>());

        string texto = painel.ToJsonString();
        Assert.DoesNotContain("cpf", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(titular.Email, texto, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(HttpStatusCode.Forbidden, (await comConta.Equipe().GetAsync("/api/v1/admin/dashboard", Ct)).StatusCode);
    }

    private async Task<Guid> ContratoAtivoAsync(FunerariaDeTeste funeraria)
    {
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync();
        Guid idContrato = await cliente.ContratarAsync(await funeraria.CriarPlanoAsync(carenciaDias: 0));
        await factory.AtivarContratoAsync(idContrato);
        return idContrato;
    }

    private async Task<(Guid IdContrato, decimal Valor)> ContratoComFaturaAsync(FunerariaDeTeste funeraria)
    {
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync();
        Guid idContrato = await cliente.ContratarAsync(await funeraria.CriarPlanoAsync(carenciaDias: 0));
        await AguardarAsync(async () => (await FaturasAsync(funeraria.Tenant, idContrato)).Count == 1, "primeira fatura");
        return (idContrato, (await FaturasAsync(funeraria.Tenant, idContrato)).Single().Valor);
    }

    private async Task FuneralAsync(FunerariaDeTeste funeraria, Guid idContrato)
    {
        await factory.AtivarContratoAsync(idContrato);
        HttpClient equipe = funeraria.Equipe();
        JsonObject contrato = (await equipe.GetFromJsonAsync<JsonObject>($"/api/v1/contratos/{idContrato}", Json, Ct))!;
        Guid titular = contrato["pessoas_cobertas"]!.AsArray().First(p => p!["tipo"]!.GetValue<string>() == "TITULAR")!["id_pessoa_coberta"]!.GetValue<Guid>();
        Guid idBeneficiario = (await Cenarios.LerAsync(await equipe.PostAsJsonAsync("/api/v1/beneficiarios", new
        {
            id_pessoa_coberta = titular,
            data_falecimento = DateOnly.FromDateTime(DateTime.Now),
            local_falecimento = "Hospital Central",
            responsavel = new { nome = "Ana Silva", telefone = "(44) 98888-7777", parentesco = "Filha" }
        }, Json, Ct), HttpStatusCode.Created))["id_beneficiario"]!.GetValue<Guid>();
        await Cenarios.LerAsync(await equipe.PostAsJsonAsync("/api/v1/funerais",
            new { id_beneficiario = idBeneficiario, data_hora_prevista = DateTime.UtcNow.AddDays(1), destinacao = "SEPULTAMENTO" }, Json, Ct), HttpStatusCode.Created);
    }

    private async Task<string> CadastrarContaAsync(FunerariaDeTeste funeraria)
    {
        await Cenarios.LerAsync(
            await funeraria.Equipe().PostAsJsonAsync("/api/v1/gestao/pagamentos/conta/cadastro",
                new { url_retorno = "https://demo.localhost/pronto", url_renovar = "https://demo.localhost/renovar" }, Json, Ct),
            HttpStatusCode.OK);
        string idConta = $"acct_{funeraria.Tenant:N}";
        Fake.LiberarConta(idConta);
        await funeraria.Equipe().GetAsync("/api/v1/gestao/pagamentos/conta", Ct);
        return idConta;
    }

    private async Task<IReadOnlyList<Fatura>> FaturasAsync(Guid tenant, Guid idContrato)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);
        return await scope.ServiceProvider.GetRequiredService<IFaturaRepository>().ListarDoContratoAsync(idContrato, Ct);
    }

    private async Task InadimplenciaAsync(Guid tenant)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);
        await scope.ServiceProvider.GetRequiredService<RotinasDeCobranca>().AplicarInadimplenciaAsync(tenant, Ct);
    }

    private async Task WebhookAsync(string tipo, string conta, string idObjeto)
    {
        string corpo = JsonSerializer.Serialize(new { id = $"evt_{Guid.NewGuid():N}", @object = "event", type = tipo, account = conta, data = new { @object = new { id = idObjeto } } });
        long agora = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        byte[] assinatura = HMACSHA256.HashData(Encoding.UTF8.GetBytes(FunerariaApiFactory.SegredoWebhookStripe), Encoding.UTF8.GetBytes($"{agora}.{corpo}"));
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Stripe-Signature", $"t={agora},v1={Convert.ToHexStringLower(assinatura)}");
        using HttpResponseMessage resposta = await client.PostAsync("/api/v1/webhooks/stripe", new StringContent(corpo, Encoding.UTF8, "application/json"), Ct);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

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
