using Microsoft.AspNetCore.Builder;

namespace Funeraria.BuildingBlocks.Infrastructure.Endpoints;

/// <summary>Texto do 403 de um endpoint, quando o card pede uma mensagem própria (ex.: AUT-07).</summary>
public sealed record MensagemAcessoNegado(string Mensagem);

public static class MensagemAcessoNegadoExtensions
{
    public static TBuilder ComMensagemAcessoNegado<TBuilder>(this TBuilder builder, string mensagem)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new MensagemAcessoNegado(mensagem));
}
