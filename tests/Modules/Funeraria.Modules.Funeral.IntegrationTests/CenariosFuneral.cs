using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Funeraria.Testing;

namespace Funeraria.Modules.Funeral.IntegrationTests;

/// <summary>Funeral pronto para os testes de locais, itens e pedidos: contrato ativo sem carência, beneficiário e funeral.</summary>
internal static class CenariosFuneral
{
    public static async Task<FuneralDeTeste> CriarFuneralAsync(this FuneralApiFactory factory, FunerariaDeTeste funeraria, Guid? plano = null)
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
            data_falecimento = DateOnly.FromDateTime(DateTime.Now),
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

        return new FuneralDeTeste(idFuneral, idContrato, cliente);
    }
}

internal sealed record FuneralDeTeste(Guid IdFuneral, Guid IdContrato, ClienteDeTeste Cliente);
