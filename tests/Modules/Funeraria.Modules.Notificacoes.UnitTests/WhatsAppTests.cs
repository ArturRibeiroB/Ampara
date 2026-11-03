using Funeraria.Modules.Notificacoes.Application.WhatsApp;
using Funeraria.Modules.Notificacoes.Domain.WhatsApp;

namespace Funeraria.Modules.Notificacoes.UnitTests;

/// <summary>NOT-03: número no formato do WhatsApp, status que só avançam e consentimento revogável.</summary>
public sealed class WhatsAppTests
{
    [Theory]
    [InlineData("(44) 99999-0000", "5544999990000")]
    [InlineData("4433334444", "554433334444")]
    [InlineData("+55 44 99999-0000", "5544999990000")]
    [InlineData("123", null)]
    [InlineData("", null)]
    [InlineData("(44) 9999A-0000", null)]
    public void Telefone_vira_e164_sem_o_mais(string informado, string? esperado) =>
        Assert.Equal(esperado, TelefoneWhatsApp.Normalizar(informado));

    [Fact]
    public void Status_so_avanca_e_falha_vale_sempre()
    {
        MensagemWhatsApp mensagem = MensagemWhatsApp.Criar(Guid.NewGuid(), Guid.NewGuid(), ModelosTeste.Lembrete, "5544999990000", ["a"], DateTime.UtcNow);
        mensagem.Enviada("wamid.1", DateTime.UtcNow);

        mensagem.AtualizarStatus(StatusMensagemWhatsApp.Lida, null, DateTime.UtcNow);
        mensagem.AtualizarStatus(StatusMensagemWhatsApp.Entregue, null, DateTime.UtcNow);
        Assert.Equal(StatusMensagemWhatsApp.Lida, mensagem.Status);

        mensagem.AtualizarStatus(StatusMensagemWhatsApp.Falhou, "Número sem WhatsApp", DateTime.UtcNow);
        mensagem.AtualizarStatus(StatusMensagemWhatsApp.Entregue, null, DateTime.UtcNow);
        Assert.Equal(StatusMensagemWhatsApp.Falhou, mensagem.Status);
        Assert.Equal("Número sem WhatsApp", mensagem.Erro);
    }

    [Fact]
    public void Consentimento_revogado_pode_ser_dado_de_novo_com_outro_numero()
    {
        DateTime agora = DateTime.UtcNow;
        var consentimento = ConsentimentoWhatsApp.Aceitar(Guid.NewGuid(), Guid.NewGuid(), "5544999990000", agora, "10.0.0.1");

        consentimento.Revogar(agora.AddDays(1));
        Assert.False(consentimento.Ativo);

        consentimento.AceitarDeNovo("5511988887777", agora.AddDays(2), "10.0.0.2");
        Assert.True(consentimento.Ativo);
        Assert.Equal("5511988887777", consentimento.Telefone);
        Assert.Equal("10.0.0.2", consentimento.Ip);
    }

    private static class ModelosTeste
    {
        public const string Lembrete = "lembrete_fatura";
    }
}
