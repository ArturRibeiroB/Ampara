using System.Data.Common;
using Dapper;
using FluentValidation;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Application.Paginacao;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Identidade.Application.Abstractions;
using Funeraria.Modules.Identidade.Domain.Equipe;
using Funeraria.Modules.Identidade.Domain.Sessoes;
using Funeraria.Modules.Identidade.Domain.Vinculos;

namespace Funeraria.Modules.Identidade.Application.Equipe;

/// <summary>Ativa ou desativa o acesso de alguém da equipe a esta funerária (AUT-08).</summary>
public sealed record AlterarStatusMembroCommand(Guid IdUsuario, bool? Ativo) : ICommand;

/// <summary>Troca o perfil de alguém da equipe: FUNERARIA, FUNCIONARIO ou CONSULTOR (AUT-08, DEC-11).</summary>
public sealed record AlterarPerfilMembroCommand(Guid IdUsuario, PerfilVinculo? Perfil) : ICommand;

/// <summary>
/// Equipe da funerária, com filtros por perfil (FUNERARIA, FUNCIONARIO, CONSULTOR), status (ATIVO, INATIVO) e
/// nome ou e-mail (AUT-08). Clientes e terceiros ficam de fora.
/// </summary>
public sealed record ListarEquipeQuery(PaginaRequest Pagina, string? Perfil, string? Status, string? Busca)
    : IQuery<PaginaResult<MembroEquipeResponse>>;

internal static class FiltrosEquipe
{
    public static PerfilVinculo? Perfil(string? valor) =>
        Enum.TryParse(valor, ignoreCase: true, out PerfilVinculo perfil) && Enum.IsDefined(perfil) && PerfisEquipe.Contem(perfil)
            ? perfil
            : null;

    public static bool? Ativo(string? valor) => valor?.Trim().ToUpperInvariant() switch
    {
        "ATIVO" => true,
        "INATIVO" => false,
        _ => null
    };
}

internal sealed class AlterarStatusMembroCommandValidator : AbstractValidator<AlterarStatusMembroCommand>
{
    public AlterarStatusMembroCommandValidator() =>
        RuleFor(c => c.Ativo).NotNull().WithMessage("Informe se o acesso fica ativo (true) ou não (false).");
}

internal sealed class AlterarPerfilMembroCommandValidator : AbstractValidator<AlterarPerfilMembroCommand>
{
    public AlterarPerfilMembroCommandValidator() =>
        RuleFor(c => c.Perfil)
            .Must(p => p is not null && PerfisEquipe.Contem(p.Value))
            .WithMessage("Perfil inválido. Use FUNERARIA, FUNCIONARIO ou CONSULTOR.");
}

internal sealed class ListarEquipeQueryValidator : AbstractValidator<ListarEquipeQuery>
{
    public ListarEquipeQueryValidator()
    {
        RuleFor(q => q.Perfil)
            .Must(p => p is null || FiltrosEquipe.Perfil(p) is not null)
            .WithMessage("Perfil inválido. Use FUNERARIA, FUNCIONARIO ou CONSULTOR.");
        RuleFor(q => q.Status)
            .Must(s => s is null || FiltrosEquipe.Ativo(s) is not null)
            .WithMessage("Status inválido. Use ATIVO ou INATIVO.");
    }
}

public sealed class AlterarStatusMembroCommandHandler(
    IVinculoRepository vinculos,
    IRefreshTokenRepository refreshTokens,
    IIdentidadeUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    ICurrentUser usuarioAtual,
    TimeProvider timeProvider)
    : ICommandHandler<AlterarStatusMembroCommand>
{
    public async ValueTask<Result> Handle(AlterarStatusMembroCommand command, CancellationToken cancellationToken)
    {
        Guid idTenant = tenantContext.RequiredTenantId;
        UsuarioVinculo? vinculo = await vinculos.ObterAsync(command.IdUsuario, idTenant, cancellationToken);
        if (vinculo is null || !PerfisEquipe.Contem(vinculo.Perfil))
        {
            return EquipeErros.UsuarioNaoEncontrado;
        }

        bool ativo = command.Ativo!.Value;
        if (!ativo && vinculo.Ativo)
        {
            if (command.IdUsuario == usuarioAtual.IdUsuario)
            {
                return EquipeErros.ProprioAcesso;
            }

            if (vinculo.Perfil == PerfilVinculo.Funeraria && await vinculos.ContarGestoresAtivosAsync(idTenant, cancellationToken) <= 1)
            {
                return EquipeErros.UltimoGestor;
            }

            // Desativar derruba só as sessões desta funerária; os outros vínculos da pessoa continuam valendo.
            DateTime agora = timeProvider.GetUtcNow().UtcDateTime;
            foreach (RefreshToken sessao in await refreshTokens.ListarAtivosDoUsuarioNaFunerariaAsync(vinculo.IdUsuario, idTenant, agora, cancellationToken))
            {
                sessao.Revogar(agora);
            }
        }

        vinculo.AlterarStatus(ativo);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class AlterarPerfilMembroCommandHandler(
    IVinculoRepository vinculos,
    IIdentidadeUnitOfWork unitOfWork,
    ITenantContext tenantContext)
    : ICommandHandler<AlterarPerfilMembroCommand>
{
    public async ValueTask<Result> Handle(AlterarPerfilMembroCommand command, CancellationToken cancellationToken)
    {
        Guid idTenant = tenantContext.RequiredTenantId;
        UsuarioVinculo? vinculo = await vinculos.ObterAsync(command.IdUsuario, idTenant, cancellationToken);
        if (vinculo is null || !PerfisEquipe.Contem(vinculo.Perfil))
        {
            return EquipeErros.UsuarioNaoEncontrado;
        }

        PerfilVinculo perfil = command.Perfil!.Value;
        bool rebaixaGestorAtivo = vinculo.Perfil == PerfilVinculo.Funeraria && perfil != PerfilVinculo.Funeraria && vinculo.Ativo;
        if (rebaixaGestorAtivo && await vinculos.ContarGestoresAtivosAsync(idTenant, cancellationToken) <= 1)
        {
            return EquipeErros.UltimoGestor;
        }

        // O novo perfil vale na próxima renovação da sessão, que relê o vínculo (AUT-03).
        vinculo.AlterarPerfil(perfil);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class ListarEquipeQueryHandler(IDbConnectionFactory connectionFactory, ITenantContext tenantContext)
    : IQueryHandler<ListarEquipeQuery, PaginaResult<MembroEquipeResponse>>
{
    // Os vínculos não usam o filtro global de tenant: o id_tenant vai explícito no WHERE (TEN-01).
    private const string Sql =
        """
        SELECT u.id_usuario     AS IdUsuario,
               f.id_funcionario AS IdFuncionario,
               COALESCE(f.nome, u.nome) AS Nome,
               u.email          AS Email,
               v.perfil         AS Perfil,
               v.ativo          AS Ativo,
               f.cpf            AS Cpf,
               f.cargo          AS Cargo,
               f.telefone       AS Telefone,
               count(*) OVER () AS Total
        FROM identidade.usuario_vinculos v
        JOIN identidade.usuarios u ON u.id_usuario = v.id_usuario
        LEFT JOIN identidade.funcionarios f ON f.id_usuario = v.id_usuario AND f.id_tenant = v.id_tenant
        WHERE v.id_tenant = @IdTenant
          AND v.perfil = ANY(@Perfis)
          AND (@Ativo::boolean IS NULL OR v.ativo = @Ativo)
          AND (@Busca::text IS NULL OR COALESCE(f.nome, u.nome) ILIKE @Busca OR u.email ILIKE @Busca)
        ORDER BY COALESCE(f.nome, u.nome), u.id_usuario
        LIMIT @Tamanho OFFSET @Deslocamento
        """;

    private static readonly string[] PerfisDaEquipe =
        [nameof(PerfilVinculo.Funeraria), nameof(PerfilVinculo.Funcionario), nameof(PerfilVinculo.Consultor)];

    public async ValueTask<Result<PaginaResult<MembroEquipeResponse>>> Handle(ListarEquipeQuery query, CancellationToken cancellationToken)
    {
        PaginaRequest pagina = query.Pagina.Normalizada();
        string? busca = string.IsNullOrWhiteSpace(query.Busca)
            ? null
            : $"%{query.Busca.Trim().Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_")}%";

        await using DbConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        List<Linha> linhas = (await connection.QueryAsync<Linha>(new CommandDefinition(
            Sql,
            new
            {
                IdTenant = tenantContext.RequiredTenantId,
                Perfis = FiltrosEquipe.Perfil(query.Perfil) is PerfilVinculo p ? new[] { p.ToString() } : PerfisDaEquipe,
                Ativo = FiltrosEquipe.Ativo(query.Status),
                Busca = busca,
                pagina.Tamanho,
                pagina.Deslocamento
            },
            cancellationToken: cancellationToken))).ToList();

        long total = linhas.Count > 0 ? linhas[0].Total : 0;
        return new PaginaResult<MembroEquipeResponse>([.. linhas.Select(l => l.Membro)], pagina.Pagina, pagina.Tamanho, total);
    }

    private sealed record Linha
    {
        public Guid IdUsuario { get; init; }

        public Guid? IdFuncionario { get; init; }

        public required string Nome { get; init; }

        public required string Email { get; init; }

        public required string Perfil { get; init; }

        public bool Ativo { get; init; }

        public string? Cpf { get; init; }

        public string? Cargo { get; init; }

        public string? Telefone { get; init; }

        public long Total { get; init; }

        public MembroEquipeResponse Membro => new(
            IdUsuario, IdFuncionario, Nome, Email, Enum.Parse<PerfilVinculo>(Perfil), Ativo, Cpf?.Trim(), Cargo, Telefone);
    }
}
