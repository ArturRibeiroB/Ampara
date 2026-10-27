using System.Security.Cryptography;
using System.Text;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace Funeraria.BuildingBlocks.Infrastructure.Seguranca;

/// <summary>
/// Cifra com o Data Protection do ASP.NET (AES-256 com chave rotacionada; as chaves ficam no banco, fora do código) e
/// hash com HMAC-SHA256 com a chave secreta <c>Seguranca:ChaveHash</c> (SEG-01).
/// </summary>
internal sealed class CriptografiaDados(IDataProtectionProvider provider, IOptions<CriptografiaOptions> options) : ICriptografiaDados
{
    private const string Prefixo = "dp1:";

    private readonly IDataProtector _protector = provider.CreateProtector("Funeraria.DadosSensiveis.v1");
    private readonly byte[] _chaveHash = Convert.FromBase64String(options.Value.ChaveHash);

    public string Cifrar(string valor) => Prefixo + _protector.Protect(valor);

    public string Decifrar(string valor) =>
        valor.StartsWith(Prefixo, StringComparison.Ordinal) ? _protector.Unprotect(valor[Prefixo.Length..]) : valor;

    public string Hash(string valor) => Convert.ToHexStringLower(HMACSHA256.HashData(_chaveHash, Encoding.UTF8.GetBytes(valor)));
}

/// <summary>Seção <c>Seguranca</c>. A chave do hash vai em secret: trocá-la invalida as buscas pelos hashes já gravados.</summary>
public sealed class CriptografiaOptions
{
    public const string Section = "Seguranca";

    /// <summary>Base64 de pelo menos 32 bytes.</summary>
    public string ChaveHash { get; set; } = string.Empty;

    public static bool Valida(CriptografiaOptions o)
    {
        try
        {
            return Convert.FromBase64String(o.ChaveHash).Length >= 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
