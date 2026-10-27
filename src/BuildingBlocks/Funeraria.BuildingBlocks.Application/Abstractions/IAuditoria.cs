namespace Funeraria.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Registro de quem fez o quê nas operações sensíveis (AUT-09). Implementada pela Identidade, dona da tabela; os outros
/// módulos chamam esta interface depois de gravar a operação, sem acessar a tabela (INF-01).
/// Usuário, funerária, IP e data vêm da requisição atual.
/// </summary>
public interface IAuditoria
{
    Task RegistrarAsync(RegistroAuditoria registro, CancellationToken cancellationToken = default);
}

/// <param name="Acao">O que foi feito, no padrão ENTIDADE_VERBO (ex.: CONTRATO_CRIADO). Lista em <see cref="AcoesAuditadas"/>.</param>
/// <param name="Entidade">Tipo do registro afetado (ex.: Contrato).</param>
/// <param name="IdEntidade">Registro afetado.</param>
/// <param name="ValoresAnteriores">Estado antes da mudança, só com os campos relevantes (sem senha, sem documento completo).</param>
/// <param name="ValoresNovos">Estado depois da mudança.</param>
/// <param name="IdTenant">Funerária afetada, quando não é a da requisição (ex.: o ADMIN suspendendo uma funerária).</param>
public sealed record RegistroAuditoria(
    string Acao,
    string Entidade,
    Guid? IdEntidade,
    object? ValoresAnteriores = null,
    object? ValoresNovos = null,
    Guid? IdTenant = null);

/// <summary>Operações auditadas (AUT-09). Novas operações sensíveis entram aqui.</summary>
public static class AcoesAuditadas
{
    public const string FuncionarioCadastrado = "FUNCIONARIO_CADASTRADO";
    public const string UsuarioStatusAlterado = "USUARIO_STATUS_ALTERADO";
    public const string UsuarioPerfilAlterado = "USUARIO_PERFIL_ALTERADO";
    public const string FunerariaStatusAlterado = "FUNERARIA_STATUS_ALTERADO";
    public const string ContratoCriado = "CONTRATO_CRIADO";
    public const string TerceiroCadastrado = "TERCEIRO_CADASTRADO";
    public const string TerceiroStatusAlterado = "TERCEIRO_STATUS_ALTERADO";
    public const string AtribuicaoAceita = "ATRIBUICAO_ACEITA";
    public const string AtribuicaoRecusada = "ATRIBUICAO_RECUSADA";
}
