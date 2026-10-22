using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Funeraria.Testing;

/// <summary>
/// SeaweedFS em container, compatível com S3, como no docker-compose (INF-18). Só sobe nos projetos de teste
/// que mexem com arquivos.
/// </summary>
public sealed class ArmazenamentoDeTeste : IAsyncDisposable
{
    public const string Bucket = "funeraria-testes";
    public const string AccessKey = "teste";
    public const string SecretKey = "teste-segredo";

    private const int Porta = 8333;

    private const string Configuracao = $$"""
        { "identities": [ { "name": "teste", "credentials": [ { "accessKey": "{{AccessKey}}", "secretKey": "{{SecretKey}}" } ],
          "actions": ["Admin", "Read", "Write", "List", "Tagging"] } ] }
        """;

    private readonly IContainer _container = new ContainerBuilder("chrislusf/seaweedfs:3.97")
        .WithCommand("server", "-dir=/data", "-s3", $"-s3.port={Porta}", "-s3.config=/etc/seaweedfs/s3.json", "-volume.max=0")
        .WithResourceMapping(Encoding.UTF8.GetBytes(Configuracao), "/etc/seaweedfs/s3.json")
        .WithPortBinding(Porta, true)
        .WithWaitStrategy(Wait.ForUnixContainer()
            .UntilHttpRequestIsSucceeded(r => r.ForPort(Porta).ForPath("/").ForStatusCodeMatching(_ => true)))
        .Build();

    public string ServiceUrl => $"http://{_container.Hostname}:{_container.GetMappedPublicPort(Porta)}";

    /// <summary>Sobe o container e cria o bucket. O filer demora um pouco depois da porta S3 abrir.</summary>
    public async Task IniciarAsync()
    {
        await _container.StartAsync();

        using AmazonS3Client s3 = CriarCliente();
        DateTime limite = DateTime.UtcNow.AddSeconds(60);
        while (true)
        {
            try
            {
                await s3.PutBucketAsync(Bucket);
                return;
            }
            catch (AmazonServiceException) when (DateTime.UtcNow < limite)
            {
                await Task.Delay(500);
            }
        }
    }

    public AmazonS3Client CriarCliente() => new(
        new BasicAWSCredentials(AccessKey, SecretKey),
        new AmazonS3Config { ServiceURL = ServiceUrl, ForcePathStyle = true, AuthenticationRegion = "us-east-1" });

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
