using Funeraria.BuildingBlocks.Domain.Events;

namespace Funeraria.Modules.Identidade.Domain.Vinculos;

/// <summary>Conta ganhou acesso a uma funerária. A Identidade reage enviando o convite ou o aviso de acesso.</summary>
public sealed record VinculoCriadoDomainEvent(Guid IdVinculo, Guid IdUsuario, Guid IdTenant, PerfilVinculo Perfil) : DomainEvent;
