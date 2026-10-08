namespace Funeraria.BuildingBlocks.Domain.Entities;

/// <summary>
/// Marca as entidades que recebem as colunas created_at, updated_at e created_by.
/// As colunas são propriedades de sombra preenchidas pelo interceptor do EF Core (INF-03),
/// para o domínio não precisar carregar dados de infraestrutura.
/// </summary>
public interface IAuditableEntity;
