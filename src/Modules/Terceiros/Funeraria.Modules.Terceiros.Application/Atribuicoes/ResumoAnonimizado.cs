using System.Text.Json.Nodes;
using Funeraria.BuildingBlocks.Application.Serialization;
using Funeraria.BuildingBlocks.Domain.Lgpd;

namespace Funeraria.Modules.Terceiros.Application.Atribuicoes;

/// <summary>
/// Reescreve o JSON do resumo (<see cref="Contracts.ResumoAtribuicao"/>) sem os dados das pessoas, mantendo o formato: o
/// portal do terceiro continua lendo o histórico. Horários, itens e quantidades ficam.
/// </summary>
internal static class ResumoAnonimizado
{
    private static readonly string[] Textos = ["origem", "destino", "local_obito", "local"];

    public static string De(string resumo)
    {
        if (JsonNode.Parse(resumo) is not JsonObject json)
        {
            return resumo;
        }

        json["nome_falecido"] = Anonimizacao.Nome;
        foreach (string campo in Textos.Where(json.ContainsKey))
        {
            json[campo] = Anonimizacao.Texto;
        }

        if (json.ContainsKey("contato"))
        {
            json["contato"] = new JsonObject { ["nome"] = Anonimizacao.Nome, ["telefone"] = string.Empty };
        }

        // Os arquivos do óbito são excluídos pelo módulo Documentos.
        if (json.ContainsKey("documentos"))
        {
            json["documentos"] = new JsonArray();
        }

        return json.ToJsonString(JsonbOptions.Default);
    }
}
