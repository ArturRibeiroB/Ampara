using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.Testing;

namespace Funeraria.Modules.Funeral.IntegrationTests;

/// <summary>FUN-05: locais, agenda e reserva de salas sem conflito de horário.</summary>
[Collection(ApiCollection.Nome)]
public sealed class LocaisTests(FuneralApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = JsonApi.Opcoes;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Amanhã, numa hora cheia (UTC), para as contas de horário ficarem legíveis.</summary>
    private static DateTime Amanha(int hora, int minuto = 0) => DateTime.UtcNow.Date.AddDays(1).AddHours(hora).AddMinutes(minuto);

    [Fact]
    public async Task Sala_propria_nao_aceita_conflito_com_o_intervalo_de_preparacao()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        HttpClient gestor = funeraria.Equipe();
        HttpClient funcionario = funeraria.Equipe(Perfis.Funcionario);
        Guid sala = await CriarLocalAsync(gestor, "Sala 1", "SALA_VELORIO", proprio: true);
        Guid cemiterio = await CriarLocalAsync(gestor, "Cemitério da Paz", "CEMITERIO", proprio: false);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await funcionario.PostAsJsonAsync("/api/v1/gestao/locais", new { nome = "Sala 2", tipo = "SALA_VELORIO", proprio = true }, Json, Ct)).StatusCode);

        FuneralDeTeste a = await factory.CriarFuneralAsync(funeraria);
        FuneralDeTeste b = await factory.CriarFuneralAsync(funeraria);

        Assert.Equal(HttpStatusCode.Created, (await ReservarAsync(funcionario, a.IdFuneral, sala, Amanha(10), Amanha(12))).StatusCode);

        // A sala fica ocupada até 13h (12h + 60 min de preparação).
        Assert.Equal(HttpStatusCode.Conflict, (await ReservarAsync(funcionario, b.IdFuneral, sala, Amanha(12, 30), Amanha(13, 30))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await ReservarAsync(funcionario, b.IdFuneral, sala, Amanha(8), Amanha(9, 30))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await ReservarAsync(funcionario, b.IdFuneral, sala, Amanha(13), Amanha(15))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ReservarAsync(funcionario, b.IdFuneral, sala, Amanha(18), Amanha(17))).StatusCode);

        // Local de terceiros (cemitério) só registra onde e quando: aceita horários sobrepostos.
        Assert.Equal(HttpStatusCode.Created, (await ReservarAsync(funcionario, a.IdFuneral, cemiterio, Amanha(14), Amanha(15))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await ReservarAsync(funcionario, b.IdFuneral, cemiterio, Amanha(14), Amanha(15))).StatusCode);

        JsonObject agenda = (await funcionario.GetFromJsonAsync<JsonObject>(
            $"/api/v1/gestao/locais/{sala}/agenda?data={DateOnly.FromDateTime(Amanha(0)):yyyy-MM-dd}", Json, Ct))!;
        JsonArray reservas = agenda["reservas"]!.AsArray();
        Assert.Equal(2, reservas.Count);
        Assert.Equal("João da Silva", reservas[0]!["nome_falecido"]!.GetValue<string>());
        Assert.Equal(Amanha(13), reservas[0]!["ocupado_ate"]!.GetValue<DateTime>().ToUniversalTime());

        JsonObject detalhe = (await a.Cliente.Http.GetFromJsonAsync<JsonObject>($"/api/v1/funerais/{a.IdFuneral}", Json, Ct))!;
        Assert.Equal(["Cemitério da Paz", "Sala 1"], detalhe["reservas"]!.AsArray().Select(r => r!["nome_local"]!.GetValue<string>()).Order());

        // Funeral cancelado libera a sala (pelo outbox).
        await funcionario.PatchAsJsonAsync($"/api/v1/funerais/{a.IdFuneral}/status", new { status = "CANCELADO" }, Json, Ct);
        for (int i = 0; i < 75 && reservas.Count == 2; i++)
        {
            await Task.Delay(200, Ct);
            reservas = (await funcionario.GetFromJsonAsync<JsonObject>(
                $"/api/v1/gestao/locais/{sala}/agenda?data={DateOnly.FromDateTime(Amanha(0)):yyyy-MM-dd}", Json, Ct))!["reservas"]!.AsArray();
        }

        Assert.Equal(b.IdFuneral, Assert.Single(reservas)!["id_funeral"]!.GetValue<Guid>());
    }

    [Fact]
    public async Task Duas_reservas_ao_mesmo_tempo_para_o_mesmo_horario_so_uma_passa()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        Guid sala = await CriarLocalAsync(funeraria.Equipe(), "Sala Concorrida", "SALA_VELORIO", proprio: true);
        FuneralDeTeste a = await factory.CriarFuneralAsync(funeraria);
        FuneralDeTeste b = await factory.CriarFuneralAsync(funeraria);

        HttpResponseMessage[] respostas = await Task.WhenAll(
            ReservarAsync(funeraria.Equipe(), a.IdFuneral, sala, Amanha(20), Amanha(22)),
            ReservarAsync(funeraria.Equipe(), b.IdFuneral, sala, Amanha(21), Amanha(23)));

        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], respostas.Select(r => r.StatusCode).Order());

        // A reserva cancelada sai da restrição: o horário volta a ficar livre.
        HttpResponseMessage vencedora = respostas.Single(r => r.StatusCode == HttpStatusCode.Created);
        Guid idReserva = (await vencedora.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["id_reserva"]!.GetValue<Guid>();
        Guid donoDaReserva = vencedora.RequestMessage!.RequestUri!.AbsolutePath.Contains(a.IdFuneral.ToString(), StringComparison.Ordinal)
            ? a.IdFuneral
            : b.IdFuneral;
        Assert.Equal(HttpStatusCode.NoContent, (await funeraria.Equipe().DeleteAsync($"/api/v1/funerais/{donoDaReserva}/reservas/{idReserva}", Ct)).StatusCode);
        Guid outro = donoDaReserva == a.IdFuneral ? b.IdFuneral : a.IdFuneral;
        Assert.Equal(HttpStatusCode.Created, (await ReservarAsync(funeraria.Equipe(), outro, sala, Amanha(20), Amanha(22))).StatusCode);
    }

    private static async Task<Guid> CriarLocalAsync(HttpClient gestor, string nome, string tipo, bool proprio)
    {
        HttpResponseMessage response = await gestor.PostAsJsonAsync("/api/v1/gestao/locais", new { nome, tipo, proprio }, Json, Ct);
        return (await Cenarios.LerAsync(response, HttpStatusCode.Created))["id_local"]!.GetValue<Guid>();
    }

    private static Task<HttpResponseMessage> ReservarAsync(HttpClient http, Guid idFuneral, Guid idLocal, DateTime inicio, DateTime fim) =>
        http.PostAsJsonAsync($"/api/v1/funerais/{idFuneral}/reservas", new { id_local = idLocal, inicio, fim }, Json, Ct);
}
