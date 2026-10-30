using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Infrastructure.Database;
using Funeraria.Modules.Financeiro.Application.Abstractions;
using Funeraria.Modules.Financeiro.Domain.Cobranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Funeraria.Modules.Financeiro.Infrastructure.Database;

public sealed class FinanceiroDbContext(DbContextOptions<FinanceiroDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext), IFinanceiroUnitOfWork
{
    public const string SchemaName = "financeiro";

    public override string Schema => SchemaName;

    internal DbSet<ContaPagamento> Contas => Set<ContaPagamento>();

    internal DbSet<ClienteStripe> Clientes => Set<ClienteStripe>();

    internal DbSet<Assinatura> Assinaturas => Set<Assinatura>();

    internal DbSet<Fatura> Faturas => Set<Fatura>();

    internal DbSet<LembreteFatura> Lembretes => Set<LembreteFatura>();

    internal DbSet<EventoStripe> Eventos => Set<EventoStripe>();

    protected override void ConfigureModel(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinanceiroDbContext).Assembly);
}

internal sealed class ContaPagamentoConfiguration : IEntityTypeConfiguration<ContaPagamento>
{
    public void Configure(EntityTypeBuilder<ContaPagamento> builder)
    {
        builder.ToTable("contas_pagamento");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id_conta_pagamento").ValueGeneratedNever();
        builder.Property(c => c.IdStripeAccount).HasMaxLength(100);
        builder.HasIndex(c => c.IdStripeAccount).IsUnique();
        builder.HasIndex(c => c.TenantId).IsUnique();
    }
}

internal sealed class ClienteStripeConfiguration : IEntityTypeConfiguration<ClienteStripe>
{
    public void Configure(EntityTypeBuilder<ClienteStripe> builder)
    {
        builder.ToTable("clientes_stripe");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id_cliente_stripe").ValueGeneratedNever();
        builder.Property(c => c.IdStripeCustomer).HasMaxLength(100);
        builder.HasIndex(c => new { c.TenantId, c.IdCliente }).IsUnique();
    }
}

internal sealed class AssinaturaConfiguration : IEntityTypeConfiguration<Assinatura>
{
    public void Configure(EntityTypeBuilder<Assinatura> builder)
    {
        builder.ToTable("assinaturas");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id_assinatura").ValueGeneratedNever();
        builder.Property(a => a.NomePlano).HasMaxLength(100);
        builder.Property(a => a.Periodicidade).HasConversion<string>().HasMaxLength(10);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.ValorPlano).HasPrecision(12, 2);
        builder.Property(a => a.ValorPorDependente).HasPrecision(12, 2);
        builder.Property(a => a.IdStripeCustomer).HasMaxLength(100);
        builder.Property(a => a.IdStripeSubscription).HasMaxLength(100);
        builder.Property(a => a.IdItemPlano).HasMaxLength(100);
        builder.Property(a => a.IdItemDependentes).HasMaxLength(100);
        builder.Ignore(a => a.ValorTotal);
        builder.Ignore(a => a.ProximoReajuste);

        builder.HasMany(a => a.Historico).WithOne().HasForeignKey("IdAssinatura").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.Historico).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();

        builder.HasIndex(a => a.IdContrato).IsUnique();
        builder.HasIndex(a => a.IdStripeSubscription).IsUnique().HasFilter("id_stripe_subscription IS NOT NULL");
    }
}

internal sealed class HistoricoValorAssinaturaConfiguration : IEntityTypeConfiguration<HistoricoValorAssinatura>
{
    public void Configure(EntityTypeBuilder<HistoricoValorAssinatura> builder)
    {
        builder.ToTable("assinaturas_historico_valores");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnName("id_historico").ValueGeneratedNever();
        builder.Property<Guid>("IdAssinatura").HasColumnName("id_assinatura");
        builder.Property(h => h.ValorPlano).HasPrecision(12, 2);
        builder.Property(h => h.ValorPorDependente).HasPrecision(12, 2);
        builder.Property(h => h.Motivo).HasMaxLength(100);
    }
}

internal sealed class FaturaConfiguration : IEntityTypeConfiguration<Fatura>
{
    public void Configure(EntityTypeBuilder<Fatura> builder)
    {
        builder.ToTable("faturas");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id_fatura").ValueGeneratedNever();
        builder.Property(f => f.IdStripeInvoice).HasMaxLength(100);
        builder.Property(f => f.Valor).HasPrecision(12, 2);
        builder.Property(f => f.ValorMultaJuros).HasPrecision(12, 2);
        builder.Property(f => f.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(f => f.FormaPagamento).HasConversion<string>().HasMaxLength(20);
        builder.Property(f => f.LinkPagamento).HasMaxLength(1000);
        builder.Property(f => f.ObservacaoBaixa).HasMaxLength(500);
        builder.Ignore(f => f.EmAberto);

        // COB-02: a sincronização é idempotente por este índice.
        builder.HasIndex(f => f.IdStripeInvoice).IsUnique();
        builder.HasIndex(f => new { f.IdContrato, f.DataVencimento });
        builder.HasIndex(f => new { f.TenantId, f.Status, f.DataVencimento });
    }
}

internal sealed class LembreteFaturaConfiguration : IEntityTypeConfiguration<LembreteFatura>
{
    public void Configure(EntityTypeBuilder<LembreteFatura> builder)
    {
        builder.ToTable("faturas_lembretes");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id_lembrete").ValueGeneratedNever();
        builder.HasIndex(l => new { l.IdFatura, l.DiasDoVencimento }).IsUnique();
    }
}

internal sealed class EventoStripeConfiguration : IEntityTypeConfiguration<EventoStripe>
{
    public void Configure(EntityTypeBuilder<EventoStripe> builder)
    {
        builder.ToTable("eventos_stripe");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id_evento_stripe").ValueGeneratedNever();
        builder.Property(e => e.IdEvento).HasMaxLength(100);
        builder.Property(e => e.Tipo).HasMaxLength(100);
        builder.Property(e => e.IdStripeAccount).HasMaxLength(100);
        builder.Property(e => e.Conteudo).HasColumnType("text");

        // COB-06: cada evento é processado uma única vez.
        builder.HasIndex(e => e.IdEvento).IsUnique();
    }
}

internal sealed class ContaPagamentoRepository(FinanceiroDbContext context) : IContaPagamentoRepository
{
    public Task<ContaPagamento?> ObterDaFunerariaAsync(CancellationToken cancellationToken = default) =>
        context.Contas.FirstOrDefaultAsync(cancellationToken);

    public Task<ContaPagamento?> ObterPorIdStripeAsync(string idStripeAccount, CancellationToken cancellationToken = default) =>
        context.Contas.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.IdStripeAccount == idStripeAccount, cancellationToken);

    public void Adicionar(ContaPagamento conta) => context.Contas.Add(conta);
}

internal sealed class AssinaturaRepository(FinanceiroDbContext context) : IAssinaturaRepository
{
    public Task<Assinatura?> ObterDoContratoAsync(Guid idContrato, CancellationToken cancellationToken = default) =>
        context.Assinaturas.FirstOrDefaultAsync(a => a.IdContrato == idContrato, cancellationToken);

    public Task<Assinatura?> ObterPorIdAsync(Guid idAssinatura, CancellationToken cancellationToken = default) =>
        context.Assinaturas.FirstOrDefaultAsync(a => a.Id == idAssinatura, cancellationToken);

    public Task<Assinatura?> ObterPorSubscriptionAsync(string idSubscription, CancellationToken cancellationToken = default) =>
        context.Assinaturas.FirstOrDefaultAsync(a => a.IdStripeSubscription == idSubscription, cancellationToken);

    public async Task<IReadOnlyList<Assinatura>> ListarPorStatusAsync(StatusAssinatura status, CancellationToken cancellationToken = default) =>
        await context.Assinaturas.Where(a => a.Status == status).OrderBy(a => a.Id).ToListAsync(cancellationToken);

    public Task<string?> ObterCustomerAsync(Guid idCliente, CancellationToken cancellationToken = default) =>
        context.Clientes.Where(c => c.IdCliente == idCliente).Select(c => c.IdStripeCustomer).FirstOrDefaultAsync(cancellationToken);

    public void AdicionarCustomer(ClienteStripe cliente) => context.Clientes.Add(cliente);

    public void Adicionar(Assinatura assinatura) => context.Assinaturas.Add(assinatura);
}

internal sealed class FaturaRepository(FinanceiroDbContext context) : IFaturaRepository
{
    public Task<Fatura?> ObterAsync(Guid idFatura, CancellationToken cancellationToken = default) =>
        context.Faturas.FirstOrDefaultAsync(f => f.Id == idFatura, cancellationToken);

    public Task<Fatura?> ObterPorStripeAsync(string idStripeInvoice, CancellationToken cancellationToken = default) =>
        context.Faturas.FirstOrDefaultAsync(f => f.IdStripeInvoice == idStripeInvoice, cancellationToken);

    public async Task<IReadOnlyList<Fatura>> ListarDoContratoAsync(Guid idContrato, CancellationToken cancellationToken = default) =>
        await context.Faturas.Where(f => f.IdContrato == idContrato).OrderByDescending(f => f.DataVencimento).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Fatura>> ListarEmAbertoAsync(CancellationToken cancellationToken = default) =>
        await context.Faturas
            .Where(f => f.Status == StatusFatura.Pendente || f.Status == StatusFatura.Vencida || f.Status == StatusFatura.Processando)
            .ToListAsync(cancellationToken);

    public Task<bool> LembreteEnviadoAsync(Guid idFatura, int diasDoVencimento, CancellationToken cancellationToken = default) =>
        context.Lembretes.AnyAsync(l => l.IdFatura == idFatura && l.DiasDoVencimento == diasDoVencimento, cancellationToken);

    public void AdicionarLembrete(LembreteFatura lembrete) => context.Lembretes.Add(lembrete);

    public void Adicionar(Fatura fatura) => context.Faturas.Add(fatura);
}

internal sealed class EventoStripeRepository(FinanceiroDbContext context) : IEventoStripeRepository
{
    public Task<EventoStripe?> ObterAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Eventos.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public void Adicionar(EventoStripe evento) => context.Eventos.Add(evento);
}
