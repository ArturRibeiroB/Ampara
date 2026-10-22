using System.Text;
using Funeraria.BuildingBlocks.Application.Arquivos;
using Funeraria.BuildingBlocks.Domain.Results;

namespace Funeraria.BuildingBlocks.UnitTests;

public sealed class ValidadorArquivoTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<byte[], TipoArquivo> Assinaturas() => new()
    {
        { [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2], TipoArquivo.Png },
        { [0xFF, 0xD8, 0xFF, 0xE0, 1, 2], TipoArquivo.Jpeg },
        { [.. "RIFF"u8, 0, 0, 0, 0, .. "WEBP"u8, 1], TipoArquivo.Webp },
        { [.. "%PDF-1.7\n"u8], TipoArquivo.Pdf }
    };

    [Theory]
    [MemberData(nameof(Assinaturas))]
    public void Tipo_e_reconhecido_pelos_primeiros_bytes(byte[] conteudo, TipoArquivo esperado) =>
        Assert.Equal(esperado, ValidadorArquivo.Identificar(conteudo));

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"/>")]
    [InlineData("<html><script>alert(1)</script></html>")]
    [InlineData("RIFF....WAVE")]
    public void Conteudo_desconhecido_nao_tem_tipo(string conteudo) =>
        Assert.Null(ValidadorArquivo.Identificar(Encoding.UTF8.GetBytes(conteudo)));

    [Fact]
    public async Task Arquivo_valido_sai_com_tipo_extensao_e_hash()
    {
        byte[] pdf = [.. "%PDF-1.7 conteudo"u8];

        Result<ArquivoValidado> resultado = await ValidarAsync(pdf, PoliticaArquivo.Documento);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("application/pdf", resultado.Value.ContentType);
        Assert.Equal(".pdf", resultado.Value.Extensao);
        Assert.Equal(64, resultado.Value.Hash.Length);
        Assert.Equal(pdf, resultado.Value.Conteudo);
    }

    [Fact]
    public async Task Tipo_fora_da_politica_e_recusado_mesmo_sendo_conhecido()
    {
        Result<ArquivoValidado> resultado = await ValidarAsync([.. "%PDF-1.7"u8], PoliticaArquivo.Logo);

        Assert.Equal("Tipo de arquivo não permitido. Envie PNG, JPEG, WEBP.", Mensagem(resultado));
    }

    [Fact]
    public async Task Arquivo_acima_do_limite_e_recusado_sem_ler_o_resto()
    {
        var politica = new PoliticaArquivo(1024 * 1024, new HashSet<TipoArquivo> { TipoArquivo.Pdf });
        var infinito = new StreamInfinito();

        Result<ArquivoValidado> resultado = await ValidadorArquivo.ValidarAsync(new ArquivoEnviado("a.pdf", infinito), politica, Ct);

        Assert.Equal("Arquivo maior que o permitido (máximo de 1 MB).", Mensagem(resultado));
        Assert.True(infinito.Lidos < 2 * 1024 * 1024);
    }

    [Fact]
    public async Task Arquivo_vazio_e_recusado() =>
        Assert.Equal("Arquivo vazio.", Mensagem(await ValidarAsync([], PoliticaArquivo.Documento)));

    [Theory]
    [InlineData("tenants/{0}/logo/a.png", true)]
    [InlineData("tenants/{0}/../{1}/logo/a.png", false)]
    [InlineData("tenants/{1}/logo/a.png", false)]
    [InlineData("outra-pasta/{0}/a.png", false)]
    public void Chave_pertence_so_a_pasta_da_funeraria(string modelo, bool pertence)
    {
        Guid tenant = Guid.NewGuid();
        string chave = string.Format(System.Globalization.CultureInfo.InvariantCulture, modelo, tenant, Guid.NewGuid());

        Assert.Equal(pertence, ChavesArquivo.PertenceA(chave, tenant));
    }

    private static Task<Result<ArquivoValidado>> ValidarAsync(byte[] conteudo, PoliticaArquivo politica) =>
        ValidadorArquivo.ValidarAsync(new ArquivoEnviado("arquivo", new MemoryStream(conteudo)), politica, Ct);

    private static string Mensagem(Result<ArquivoValidado> resultado)
    {
        Assert.True(resultado.IsFailure);
        var erro = Assert.IsType<ValidationError>(resultado.Error);
        return Assert.Single(erro.Erros[ValidadorArquivo.Campo]);
    }

    /// <summary>Stream sem fim, com um PDF no começo: prova que a leitura para no limite.</summary>
    private sealed class StreamInfinito : Stream
    {
        public long Lidos { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => Lidos; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Span<byte> destino = buffer.AsSpan(offset, count);
            destino.Fill((byte)'x');
            if (Lidos == 0)
            {
                "%PDF-"u8.CopyTo(destino);
            }

            Lidos += count;
            return count;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
