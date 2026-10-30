using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Funeraria.Testing;

namespace Funeraria.Modules.Funeral.IntegrationTests;

/// <summary>Funeral pronto para os testes de locais, itens e pedidos: contrato ativo sem carência, beneficiário e funeral.</summary>
internal static class CenariosFuneral
{
    public static async Task<FuneralDeTeste> CriarFuneralAsync(
        this FuneralApiFactory factory, FunerariaDeTeste funeraria, Guid? plano = null, DateOnly? dataFalecimento = null)
    {
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync();
        Guid idContrato = await cliente.ContratarAsync(plano ?? await funeraria.CriarPlanoAsync(carenciaDias: 0));
        await factory.AtivarContratoAsync(idContrato);
        HttpClient equipe = funeraria.Equipe();

        JsonObject contrato = (await equipe.GetFromJsonAsync<JsonObject>($"/api/v1/contratos/{idContrato}", JsonApi.Opcoes))!;
        Guid titular = contrato["pessoas_cobertas"]!.AsArray()
            .Single(p => p!["tipo"]!.GetValue<string>() == "TITULAR")!["id_pessoa_coberta"]!.GetValue<Guid>();

        HttpResponseMessage beneficiario = await equipe.PostAsJsonAsync("/api/v1/beneficiarios", new
        {
            id_pessoa_coberta = titular,
            data_falecimento = dataFalecimento ?? DateOnly.FromDateTime(DateTime.Now),
            local_falecimento = "Hospital Central",
            responsavel = new { nome = "Ana Silva", telefone = "(44) 98888-7777", parentesco = "Filha" }
        }, JsonApi.Opcoes);
        Guid idBeneficiario = (await Cenarios.LerAsync(beneficiario, HttpStatusCode.Created))["id_beneficiario"]!.GetValue<Guid>();

        HttpResponseMessage funeral = await equipe.PostAsJsonAsync("/api/v1/funerais", new
        {
            id_beneficiario = idBeneficiario,
            data_hora_prevista = DateTime.UtcNow.AddDays(1),
            destinacao = "SEPULTAMENTO"
        }, JsonApi.Opcoes);
        Guid idFuneral = (await Cenarios.LerAsync(funeral, HttpStatusCode.Created))["id_funeral"]!.GetValue<Guid>();

        return new FuneralDeTeste(idFuneral, idContrato, cliente, idBeneficiario);
    }

    public static Task<FornecedorDeTeste> CriarFornecedorAsync(this FuneralApiFactory factory, FunerariaDeTeste funeraria, string nome) =>
        factory.CriarTerceiroAsync(funeraria, nome, "FORNECEDOR");

    /// <summary>Terceiro com conta ativa, já logado no portal (o vínculo nasce pelo outbox).</summary>
    public static async Task<FornecedorDeTeste> CriarTerceiroAsync(this FuneralApiFactory factory, FunerariaDeTeste funeraria, string nome, string tipo)
    {
        (_, string email) = await factory.CriarUsuarioAsync();
        HttpResponseMessage criado = await funeraria.Equipe().PostAsJsonAsync("/api/v1/gestao/terceiros", new
        {
            tipo,
            email,
            nome_razao_social = nome,
            cpf_cnpj = DocumentosDeTeste.NovoCnpj()
        }, JsonApi.Opcoes);
        Guid idTerceiro = (await Cenarios.LerAsync(criado, HttpStatusCode.Created))["id_terceiro"]!.GetValue<Guid>();

        for (int i = 0; i < 75; i++)
        {
            HttpResponseMessage login = await factory.Http(funeraria.Slug).PostAsJsonAsync("/api/v1/auth/login",
                new { email, senha = FunerariaApiFactory.SenhaPadrao }, JsonApi.Opcoes);
            if (login.IsSuccessStatusCode)
            {
                string token = (await login.Content.ReadFromJsonAsync<JsonObject>(JsonApi.Opcoes))!["access_token"]!.GetValue<string>();
                return new FornecedorDeTeste(idTerceiro, factory.Http(funeraria.Slug, token));
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"O fornecedor {nome} não conseguiu entrar.");
    }

    /// <summary>Espera uma condição que depende do outbox (eventos e notificações).</summary>
    public static async Task<T> AguardarAsync<T>(Func<Task<T>> consultar, Func<T, bool> pronto, string oQue)
    {
        for (int i = 0; i < 75; i++)
        {
            T valor = await consultar();
            if (pronto(valor))
            {
                return valor;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Esperando: {oQue}.");
    }
}

internal sealed record FornecedorDeTeste(Guid IdTerceiro, HttpClient Http);

internal sealed record FuneralDeTeste(Guid IdFuneral, Guid IdContrato, ClienteDeTeste Cliente, Guid IdBeneficiario);
