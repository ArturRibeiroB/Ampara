using Funeraria.Modules.Tenants.Domain.Funerarias;

namespace Funeraria.Modules.Tenants.Endpoints;

/// <summary>Corpo de POST /onboarding/funerarias e POST /admin/tenants (TEN-02).</summary>
public sealed record CadastrarFunerariaRequest(
    string? NomeFantasia,
    string? RazaoSocial,
    string? Cnpj,
    string? Slug,
    string? EmailContato,
    string? Telefone,
    GestorRequest? Gestor);

/// <summary>Quem vai administrar a funerária. Recebe por e-mail o convite para definir a senha.</summary>
public sealed record GestorRequest(string? Nome, string? Email);

/// <summary>Corpo de PATCH /admin/tenants/{id}/status.</summary>
public sealed record AlterarStatusFunerariaRequest(StatusFuneraria? Status);

/// <summary>Corpo de PUT /gestao/configuracoes (TEN-03).</summary>
public sealed record AtualizarConfiguracoesRequest(IdentidadeVisual? IdentidadeVisual, ParametrosFuneraria? Parametros);
