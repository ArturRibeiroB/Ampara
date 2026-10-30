using Funeraria.Modules.Funeral.Domain.Locais;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Funeraria.Modules.Funeral.Infrastructure.Database;

internal sealed class LocalConfiguration : IEntityTypeConfiguration<Local>
{
    public void Configure(EntityTypeBuilder<Local> builder)
    {
        builder.ToTable("locais");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id_local").ValueGeneratedNever();
        builder.Property(l => l.Nome).HasMaxLength(150);
        builder.Property(l => l.Tipo).HasConversion<string>().HasMaxLength(20);
        builder.Property(l => l.Endereco).HasMaxLength(300);
    }
}

/// <summary>
/// A restrição de exclusão <c>ex_reservas_sem_conflito</c> (na migration ReservasDeLocais) impede no banco duas reservas
/// ativas do mesmo local próprio com períodos que se sobrepõem, mesmo quando chegam ao mesmo tempo (FUN-05).
/// </summary>
internal sealed class ReservaConfiguration : IEntityTypeConfiguration<Reserva>
{
    public const string RestricaoSemConflito = "ex_reservas_sem_conflito";

    public void Configure(EntityTypeBuilder<Reserva> builder)
    {
        builder.ToTable("reservas");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id_reserva").ValueGeneratedNever();

        builder.HasOne<Local>().WithMany().HasForeignKey(r => r.IdLocal).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.IdLocal, r.Inicio });
        builder.HasIndex(r => r.IdFuneral);
    }
}

internal sealed class LocalRepository(FuneralDbContext context) : ILocalRepository
{
    public Task<Local?> ObterAsync(Guid idLocal, CancellationToken cancellationToken = default) =>
        context.Locais.FirstOrDefaultAsync(l => l.Id == idLocal, cancellationToken);

    public async Task<IReadOnlyList<Local>> ListarAsync(bool somenteAtivos, CancellationToken cancellationToken = default) =>
        await context.Locais.AsNoTracking().Where(l => !somenteAtivos || l.Ativo).OrderBy(l => l.Tipo).ThenBy(l => l.Nome).ToListAsync(cancellationToken);

    public void Adicionar(Local local) => context.Locais.Add(local);
}

internal sealed class ReservaRepository(FuneralDbContext context) : IReservaRepository
{
    public Task<Reserva?> ObterAsync(Guid idReserva, CancellationToken cancellationToken = default) =>
        context.Reservas.FirstOrDefaultAsync(r => r.Id == idReserva, cancellationToken);

    public async Task<IReadOnlyList<Reserva>> ListarDoLocalAsync(Guid idLocal, DateTime de, DateTime ate, CancellationToken cancellationToken = default) =>
        await context.Reservas
            .Where(r => r.IdLocal == idLocal && !r.Cancelada && r.Inicio < ate && r.OcupadoAte > de)
            .OrderBy(r => r.Inicio)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Reserva>> ListarDoFuneralAsync(Guid idFuneral, CancellationToken cancellationToken = default) =>
        await context.Reservas.Where(r => r.IdFuneral == idFuneral && !r.Cancelada).OrderBy(r => r.Inicio).ToListAsync(cancellationToken);

    public void Adicionar(Reserva reserva) => context.Reservas.Add(reserva);
}
