using Funeraria.Modules.Identidade.Domain.Sessoes;

namespace Funeraria.Modules.Identidade.UnitTests.Domain;

public sealed class RefreshTokenTests
{
    private static readonly DateTime Agora = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Duracao = TimeSpan.FromDays(7);

    [Fact]
    public void Token_novo_esta_ativo_ate_expirar()
    {
        RefreshToken token = Criar();

        Assert.True(token.EstaAtivo(Agora));
        Assert.True(token.EstaAtivo(Agora + Duracao - TimeSpan.FromSeconds(1)));
        Assert.False(token.EstaAtivo(Agora + Duracao));
    }

    [Fact]
    public void Rotacionar_revoga_o_atual_e_cria_outro_na_mesma_sessao()
    {
        Guid tenant = Guid.CreateVersion7();
        RefreshToken atual = Criar(tenant);
        DateTime depois = Agora.AddHours(1);

        RefreshToken novo = atual.Rotacionar("hash-novo", depois, Duracao);

        Assert.True(atual.Revogado);
        Assert.True(atual.FoiRotacionado);
        Assert.Equal(novo.Id, atual.SubstituidoPor);
        Assert.Equal(atual.IdUsuario, novo.IdUsuario);
        Assert.Equal(tenant, novo.IdTenant);
        Assert.Equal(depois + Duracao, novo.ExpiraEm);
        Assert.True(novo.EstaAtivo(depois));
    }

    [Fact]
    public void Nao_e_possivel_rotacionar_token_revogado_ou_expirado()
    {
        RefreshToken revogado = Criar();
        revogado.Revogar(Agora);

        Assert.Throws<InvalidOperationException>(() => revogado.Rotacionar("x", Agora, Duracao));
        Assert.Throws<InvalidOperationException>(() => Criar().Rotacionar("x", Agora + Duracao, Duracao));
    }

    [Fact]
    public void Revogar_no_logout_nao_marca_como_rotacionado()
    {
        RefreshToken token = Criar();

        token.Revogar(Agora);
        token.Revogar(Agora.AddMinutes(5));

        Assert.True(token.Revogado);
        Assert.False(token.FoiRotacionado);
        Assert.Equal(Agora, token.RevogadoEm);
    }

    private static RefreshToken Criar(Guid? tenant = null) =>
        RefreshToken.Criar(Guid.CreateVersion7(), tenant, "hash", Agora, Duracao);
}
