using FluentValidation;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Clientes.Contracts;
using Funeraria.Modules.Contratos.Contracts.IntegrationEvents;
using Funeraria.Modules.Financeiro.Application.Abstractions;
using Funeraria.Modules.Financeiro.Domain.Cobranca;
using Funeraria.Modules.Tenants.Contracts;

namespace Funeraria.Modules.Financeiro.Application.Cobrancas;

/// <summary>Situação do cadastro de pagamentos da funerária (DEC-09), atualizada no Stripe na hora da consulta.</summary>
public sealed record ObterContaPagamentosQuery : IQuery<ContaPagamentosResponse>;

/// <summary>
/// Abre (ou continua) o cadastro de pagamentos: cria a conta conectada, se preciso, e devolve o link da página do Stripe onde
/// o gestor informa CNPJ, conta bancária e documentos dos sócios. O link vale por pouco tempo e é de uso único.
/// </summary>
public sealed record IniciarCadastroPagamentosCommand(string? UrlRetorno, string? UrlRenovar) : ICommand<LinkCadastroResponse>;

public sealed record ContaPagamentosResponse(string IdStripeAccount, bool CobrancasHabilitadas, bool CadastroEnviado);

public sealed record LinkCadastroResponse(string Url);

internal sealed class IniciarCadastroPagamentosCommandValidator : AbstractValidator<IniciarCadastroPagamentosCommand>
{
    public IniciarCadastroPagamentosCommandValidator()
    {
        RuleFor(c => c.UrlRetorno).Must(UrlValida).WithMessage("url_retorno deve ser um endereço http ou https.");
        RuleFor(c => c.UrlRenovar).Must(UrlValida).WithMessage("url_renovar deve ser um endereço http ou https.");
    }

    private static bool UrlValida(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}

public sealed class ObterContaPagamentosQueryHandler(IContaPagamentoRepository contas, ProvisionamentoAssinaturas provisionamento)
    : IQueryHandler<ObterContaPagamentosQuery, ContaPagamentosResponse>
{
    public async ValueTask<Result<ContaPagamentosResponse>> Handle(ObterContaPagamentosQuery query, CancellationToken cancellationToken)
    {
        ContaPagamento? conta = await contas.ObterDaFunerariaAsync(cancellationToken);
        if (conta is null)
        {
            return FaturaErros.ContaNaoCadastrada;
        }

        await provisionamento.AtualizarContaAsync(conta, cancellationToken);
        return new ContaPagamentosResponse(conta.IdStripeAccount, conta.CobrancasHabilitadas, conta.CadastroEnviado);
    }
}

public sealed class IniciarCadastroPagamentosCommandHandler(
    IContaPagamentoRepository contas,
    IPagamentoGateway gateway,
    IFinanceiroUnitOfWork unitOfWork,
    ITenantsApi tenants,
    ITenantContext tenantContext)
    : ICommandHandler<IniciarCadastroPagamentosCommand, LinkCadastroResponse>
{
    public async ValueTask<Result<LinkCadastroResponse>> Handle(IniciarCadastroPagamentosCommand command, CancellationToken cancellationToken)
    {
        Guid tenant = tenantContext.RequiredTenantId;
        ContaPagamento? conta = await contas.ObterDaFunerariaAsync(cancellationToken);
        if (conta is null)
        {
            FunerariaResumoDto? funeraria = await tenants.ObterPorIdAsync(tenant, cancellationToken);
            string idConta = await gateway.CriarContaConectadaAsync(tenant, funeraria?.NomeFantasia ?? string.Empty, cancellationToken);
            conta = ContaPagamento.Criar(tenant, idConta);
            contas.Adicionar(conta);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        string url = await gateway.CriarLinkCadastroAsync(conta.IdStripeAccount, command.UrlRetorno!, command.UrlRenovar!, cancellationToken);
        return new LinkCadastroResponse(url);
    }
}

/// <summary>
/// Liga o contrato ao Stripe (COB-01): cliente e assinatura na conta conectada. Sem conta pronta, a assinatura fica
/// AGUARDANDO_CONTA e é criada quando a funerária concluir o cadastro (ou na reconciliação diária).
/// </summary>
public sealed class ProvisionamentoAssinaturas(
    IContaPagamentoRepository contas,
    IAssinaturaRepository assinaturas,
    IPagamentoGateway gateway,
    IFinanceiroUnitOfWork unitOfWork,
    IClientesApi clientes,
    EspelhoDeFaturas espelho,
    ITenantContext tenantContext,
    TimeProvider timeProvider)
{
    public async Task ProvisionarAsync(Assinatura assinatura, CancellationToken cancellationToken)
    {
        ContaPagamento? conta = await contas.ObterDaFunerariaAsync(cancellationToken);
        if (conta is not { CobrancasHabilitadas: true } || assinatura.Status != StatusAssinatura.AguardandoConta)
        {
            return;
        }

        string? idCustomer = await assinaturas.ObterCustomerAsync(assinatura.IdCliente, cancellationToken);
        if (idCustomer is null)
        {
            TitularDto titular = await clientes.ObterTitularAsync(assinatura.IdCliente, cancellationToken)
                ?? throw new InvalidOperationException($"Titular do contrato {assinatura.IdContrato} não encontrado.");
            idCustomer = await gateway.CriarClienteAsync(conta.IdStripeAccount, titular.IdCliente, titular.Nome, titular.Email, cancellationToken);
            assinaturas.AdicionarCustomer(ClienteStripe.Criar(tenantContext.RequiredTenantId, assinatura.IdCliente, idCustomer));
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        DateOnly hoje = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        AssinaturaCriada criada = await gateway.CriarAssinaturaAsync(
            conta.IdStripeAccount,
            idCustomer,
            new NovaAssinatura(
                assinatura.IdContrato,
                assinatura.IdPlano,
                assinatura.NomePlano,
                assinatura.Periodicidade,
                CicloCobranca.PrimeiraAncora(hoje, assinatura.DiaVencimento, assinatura.Periodicidade),
                assinatura.ValorPlano,
                assinatura.ValorPorDependente,
                assinatura.QuantidadeDependentes),
            cancellationToken);

        assinatura.Vincular(idCustomer, criada.IdSubscription, criada.IdItemPlano, criada.IdItemDependentes);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // A primeira fatura já existe: não depende do webhook para aparecer ao cliente.
        await espelho.EspelharAsync(criada.PrimeiraFatura, assinatura, cancellationToken);
    }

    /// <summary>Atualiza a situação da conta; ao ficar pronta, cria as assinaturas que esperavam por ela.</summary>
    public async Task AtualizarContaAsync(ContaPagamento conta, CancellationToken cancellationToken)
    {
        SituacaoConta situacao = await gateway.ObterSituacaoContaAsync(conta.IdStripeAccount, cancellationToken);
        bool ficouPronta = conta.AtualizarSituacao(situacao.CobrancasHabilitadas, situacao.CadastroEnviado);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (ficouPronta)
        {
            await ProvisionarPendentesAsync(cancellationToken);
        }
    }

    public async Task ProvisionarPendentesAsync(CancellationToken cancellationToken)
    {
        foreach (Assinatura pendente in await assinaturas.ListarPorStatusAsync(StatusAssinatura.AguardandoConta, cancellationToken))
        {
            await ProvisionarAsync(pendente, cancellationToken);
        }
    }
}

/// <summary>Contrato criado: registra a assinatura e tenta criá-la no Stripe (COB-01). Idempotente: um por contrato.</summary>
internal sealed class CriarAssinaturaDoContrato(
    IAssinaturaRepository assinaturas,
    IFinanceiroUnitOfWork unitOfWork,
    ProvisionamentoAssinaturas provisionamento,
    TimeProvider timeProvider)
    : IIntegrationEventHandler<ContratoCriadoIntegrationEvent>
{
    public async Task Handle(ContratoCriadoIntegrationEvent evento, CancellationToken cancellationToken)
    {
        Assinatura? assinatura = await assinaturas.ObterDoContratoAsync(evento.IdContrato, cancellationToken);
        if (assinatura is null)
        {
            assinatura = Assinatura.Registrar(
                evento.IdTenant,
                new DadosAssinatura(
                    evento.IdContrato,
                    evento.IdCliente,
                    evento.IdPlano,
                    evento.NomePlano,
                    evento.Periodicidade == "ANUAL" ? PeriodicidadeCobranca.Anual : PeriodicidadeCobranca.Mensal,
                    evento.DiaVencimento,
                    evento.DataContratacao,
                    evento.ValorPlano,
                    evento.ValorPorDependente,
                    evento.QuantidadeDependentes),
                DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime));
            assinaturas.Adicionar(assinatura);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await provisionamento.ProvisionarAsync(assinatura, cancellationToken);
    }
}

/// <summary>Dependente incluído ou removido: muda a quantidade na assinatura, valendo na próxima fatura (CON-02).</summary>
internal sealed class AtualizarDependentesDaAssinatura(
    IAssinaturaRepository assinaturas,
    IContaPagamentoRepository contas,
    IPagamentoGateway gateway,
    IFinanceiroUnitOfWork unitOfWork)
    : IIntegrationEventHandler<PessoasDoContratoAlteradasIntegrationEvent>
{
    public async Task Handle(PessoasDoContratoAlteradasIntegrationEvent evento, CancellationToken cancellationToken)
    {
        Assinatura? assinatura = await assinaturas.ObterDoContratoAsync(evento.IdContrato, cancellationToken);
        if (assinatura is null || assinatura.QuantidadeDependentes == evento.QuantidadeDependentes)
        {
            return;
        }

        if (assinatura.Status == StatusAssinatura.Ativa && await contas.ObterDaFunerariaAsync(cancellationToken) is ContaPagamento conta)
        {
            await gateway.AlterarDependentesAsync(
                conta.IdStripeAccount, assinatura.IdStripeSubscription!, assinatura.IdItemDependentes!, evento.QuantidadeDependentes, cancellationToken);
        }

        assinatura.AlterarDependentes(evento.QuantidadeDependentes);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Contrato cancelado (CON-03): encerra a assinatura e cancela as faturas ainda no prazo. As vencidas continuam em aberto.</summary>
internal sealed class EncerrarAssinaturaDoContrato(
    IAssinaturaRepository assinaturas,
    IContaPagamentoRepository contas,
    IFaturaRepository faturas,
    IPagamentoGateway gateway,
    IFinanceiroUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IIntegrationEventHandler<ContratoCanceladoIntegrationEvent>
{
    public async Task Handle(ContratoCanceladoIntegrationEvent evento, CancellationToken cancellationToken)
    {
        Assinatura? assinatura = await assinaturas.ObterDoContratoAsync(evento.IdContrato, cancellationToken);
        if (assinatura is null || assinatura.Status == StatusAssinatura.Cancelada)
        {
            return;
        }

        ContaPagamento? conta = await contas.ObterDaFunerariaAsync(cancellationToken);
        if (assinatura.IdStripeSubscription is not null && conta is not null)
        {
            await gateway.CancelarAssinaturaAsync(conta.IdStripeAccount, assinatura.IdStripeSubscription, cancellationToken);

            DateOnly hoje = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
            foreach (Fatura fatura in (await faturas.ListarDoContratoAsync(evento.IdContrato, cancellationToken))
                .Where(f => f.Status == StatusFatura.Pendente && f.DataVencimento >= hoje))
            {
                await gateway.CancelarFaturaAsync(conta.IdStripeAccount, fatura.IdStripeInvoice, cancellationToken);
                fatura.Atualizar(new FaturaEspelhada(
                    fatura.IdStripeInvoice, fatura.Competencia, fatura.Valor, fatura.DataVencimento, SituacaoFaturaStripe.Cancelada,
                    fatura.LinkPagamento, null, null, fatura.Avulsa));
            }
        }

        assinatura.Encerrar();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
