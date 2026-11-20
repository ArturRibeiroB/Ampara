namespace Funeraria.BuildingBlocks.Application.Lgpd;

/// <summary>
/// Política de retenção do <c>docs/lgpd.md</c> (SEG-01). Cada módulo aplica a parte dos dados dele, e o job de retenção
/// chama todas. Nasce <b>desligada</b> e, ligada, em <b>simulação</b>: só conta o que faria, sem alterar nada. Os prazos ainda
/// precisam ser validados pelo grupo; por isso ficam em configuração (seção <c>Lgpd:Retencao</c>).
/// </summary>
public sealed class RetencaoOptions
{
    public const string Section = "Lgpd:Retencao";

    /// <summary>Liga o job. Desligado, nada roda.</summary>
    public bool Habilitada { get; set; }

    /// <summary>Só conta e registra no log o que seria anonimizado ou excluído. Para aplicar de verdade, <c>false</c>.</summary>
    public bool Simulacao { get; set; } = true;

    /// <summary>Contratos, faturas e pagamentos: anonimiza titular e dependentes depois do fim do último contrato.</summary>
    public int AnosAposFimDoContrato { get; set; } = 5;

    /// <summary>Dados do falecido, certidão e documentos do óbito: contados a partir do funeral.</summary>
    public int AnosAposFuneral { get; set; } = 5;

    /// <summary>Cadastro de cliente que nunca teve contrato, sem acesso à conta nesse período.</summary>
    public int AnosClienteSemContrato { get; set; } = 2;

    /// <summary>Linhas da auditoria (AUT-09).</summary>
    public int AnosAuditoria { get; set; } = 5;

    /// <summary>
    /// Preferências de funeral valem enquanto houver contrato. Depois que o último contrato termina, esperam estes dias
    /// (o cliente pode voltar logo) e são excluídas.
    /// </summary>
    public int DiasPreferenciasAposFimDoContrato { get; set; } = 30;

    /// <summary>Máximo de registros por política em cada execução: evita uma transação gigante na primeira vez.</summary>
    public int LimitePorExecucao { get; set; } = 500;
}

/// <summary>Uma regra da política de retenção, aplicada pelo módulo dono dos dados (INF-01).</summary>
public interface IPoliticaRetencao
{
    /// <summary>Nome curto para o log (ex.: "clientes").</summary>
    string Nome { get; }

    /// <summary>Aplica a regra, ou só conta em simulação. Devolve quantos registros foram (ou seriam) afetados.</summary>
    Task<int> AplicarAsync(ExecucaoRetencao execucao, CancellationToken cancellationToken);
}

/// <param name="Agora">Momento da execução, em UTC.</param>
/// <param name="Simulacao">Quando verdadeiro, a política só conta: não altera, não exclui e não publica eventos.</param>
/// <param name="Prazos">Prazos configurados.</param>
public sealed record ExecucaoRetencao(DateTime Agora, bool Simulacao, RetencaoOptions Prazos)
{
    public DateOnly Hoje => DateOnly.FromDateTime(Agora);
}

/// <summary>Regras de consentimento no cadastro (seção <c>Lgpd</c>, SEG-01).</summary>
public sealed class ConsentimentoOptions
{
    public const string Section = "Lgpd";

    /// <summary>
    /// Exige o aceite dos termos de uso e da política de privacidade no auto-cadastro do cliente. Desligado até o frontend
    /// mandar o campo <c>aceite_termos</c> no cadastro; ligado, o cadastro sem aceite volta 400.
    /// </summary>
    public bool AceiteTermosObrigatorio { get; set; }
}
