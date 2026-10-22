using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Arquivos;
using Funeraria.BuildingBlocks.Domain.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Funeraria.BuildingBlocks.Infrastructure.Arquivos;

/// <summary>
/// Provider S3 do armazenamento (INF-18): atende AWS S3 e serviços compatíveis, como o SeaweedFS do ambiente
/// de desenvolvimento. O bucket é privado; o navegador só baixa por link pré-assinado, que expira.
/// </summary>
internal sealed class ArmazenamentoS3(ClientesS3 clientes, IOptions<ArmazenamentoOptions> options, ITenantContext tenantContext)
    : IArmazenamentoArquivos
{
    private readonly string _bucket = options.Value.S3.Bucket;

    public async Task<Result<ArquivoSalvo>> SalvarAsync(
        Guid idTenant,
        string pasta,
        ArquivoEnviado arquivo,
        PoliticaArquivo politica,
        CancellationToken cancellationToken = default)
    {
        Result<ArquivoValidado> validacao = await ValidadorArquivo.ValidarAsync(arquivo, politica, cancellationToken);
        if (validacao.IsFailure)
        {
            return Result.Failure<ArquivoSalvo>(validacao.Error);
        }

        ArquivoValidado validado = validacao.Value;
        string chave = $"{ChavesArquivo.Prefixo(idTenant)}{pasta.Trim('/')}/{Guid.CreateVersion7()}{validado.Extensao}";

        using var conteudo = new MemoryStream(validado.Conteudo, writable: false);
        await clientes.Interno.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = chave,
            InputStream = conteudo,
            ContentType = validado.ContentType,
            AutoCloseStream = false
        }, cancellationToken);

        return new ArquivoSalvo(chave, validado.ContentType, validado.Conteudo.LongLength, validado.Hash);
    }

    public async Task<ArquivoLido?> AbrirAsync(string chave, CancellationToken cancellationToken = default)
    {
        if (!Permitida(chave))
        {
            return null;
        }

        try
        {
            using GetObjectResponse resposta = await clientes.Interno.GetObjectAsync(_bucket, chave, cancellationToken);
            var conteudo = new MemoryStream();
            await resposta.ResponseStream.CopyToAsync(conteudo, cancellationToken);
            conteudo.Position = 0;
            return new ArquivoLido(conteudo, resposta.Headers.ContentType ?? "application/octet-stream", conteudo.Length);
        }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task RemoverAsync(string chave, CancellationToken cancellationToken = default)
    {
        if (Permitida(chave))
        {
            await clientes.Interno.DeleteObjectAsync(_bucket, chave, cancellationToken);
        }
    }

    public async Task<Uri> GerarLinkTemporarioAsync(string chave, TimeSpan validade, CancellationToken cancellationToken = default)
    {
        if (!Permitida(chave))
        {
            throw new InvalidOperationException("Arquivo de outra funerária.");
        }

        string url = await clientes.Publico.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = chave,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(validade),
            Protocol = clientes.ProtocoloPublico
        });

        return new Uri(url);
    }

    /// <summary>
    /// Numa requisição de funerária, só os arquivos dela. Sem funerária (ADMIN, jobs), o chamador responde pela chave.
    /// </summary>
    private bool Permitida(string chave) =>
        chave.StartsWith("tenants/", StringComparison.Ordinal)
        && (tenantContext.TenantId is not Guid idTenant || ChavesArquivo.PertenceA(chave, idTenant));
}

/// <summary>
/// Dois clientes: um para a API falar com o armazenamento (ex.: http://seaweedfs:8333 dentro do Docker) e outro só
/// para assinar links com o endereço que o navegador alcança (ex.: http://localhost:8333). Assinar não faz chamada.
/// </summary>
internal sealed class ClientesS3 : IDisposable
{
    public ClientesS3(IOptions<ArmazenamentoOptions> options)
    {
        S3Options s3 = options.Value.S3;
        Interno = Criar(s3, s3.ServiceUrl);
        string? urlPublica = string.IsNullOrWhiteSpace(s3.UrlPublica) ? s3.ServiceUrl : s3.UrlPublica;
        Publico = string.IsNullOrWhiteSpace(s3.UrlPublica) ? Interno : Criar(s3, s3.UrlPublica);

        // O SDK assina em HTTPS por padrão; num endereço http (desenvolvimento) o link precisa sair em http.
        ProtocoloPublico = urlPublica?.StartsWith("http://", StringComparison.OrdinalIgnoreCase) == true ? Protocol.HTTP : Protocol.HTTPS;
    }

    public Protocol ProtocoloPublico { get; }

    public IAmazonS3 Interno { get; }

    public IAmazonS3 Publico { get; }

    public void Dispose()
    {
        Interno.Dispose();
        if (!ReferenceEquals(Publico, Interno))
        {
            Publico.Dispose();
        }
    }

    private static AmazonS3Client Criar(S3Options s3, string? serviceUrl)
    {
        var config = new AmazonS3Config
        {
            AuthenticationRegion = s3.Regiao,
            ForcePathStyle = s3.ForcarPathStyle,

            // Serviços compatíveis com S3 nem sempre aceitam os checksums que o SDK 4 calcula por padrão.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        };

        if (string.IsNullOrWhiteSpace(serviceUrl))
        {
            config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(s3.Regiao);
        }
        else
        {
            config.ServiceURL = serviceUrl;
        }

        return string.IsNullOrWhiteSpace(s3.AccessKey)
            ? new AmazonS3Client(config)
            : new AmazonS3Client(new BasicAWSCredentials(s3.AccessKey, s3.SecretKey), config);
    }
}

/// <summary>Seção <c>Armazenamento</c>. As chaves de acesso vão em secret; sem elas, vale a credencial do ambiente (IAM).</summary>
public sealed class ArmazenamentoOptions
{
    public const string Section = "Armazenamento";

    public S3Options S3 { get; set; } = new();
}

public sealed class S3Options
{
    /// <summary>Endereço do serviço compatível com S3. Vazio para a AWS.</summary>
    public string? ServiceUrl { get; set; }

    /// <summary>Endereço usado nos links temporários, quando o navegador não alcança o <see cref="ServiceUrl"/>.</summary>
    public string? UrlPublica { get; set; }

    public string Regiao { get; set; } = "us-east-1";

    public string Bucket { get; set; } = "funeraria-documentos";

    public bool ForcarPathStyle { get; set; } = true;

    public string? AccessKey { get; set; }

    public string? SecretKey { get; set; }
}

public static class ArmazenamentoExtensions
{
    public static IServiceCollection AddArmazenamentoArquivos(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ArmazenamentoOptions>().Bind(configuration.GetSection(ArmazenamentoOptions.Section));
        services.AddSingleton<ClientesS3>();
        services.AddScoped<IArmazenamentoArquivos, ArmazenamentoS3>();
        return services;
    }
}
