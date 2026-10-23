using Funeraria.Modules.Identidade.Domain.Equipe;
using Funeraria.Modules.Identidade.Infrastructure.Database;
using Funeraria.Modules.Identidade.Infrastructure.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Funeraria.Modules.Identidade.Infrastructure.Equipe;

internal sealed class FuncionarioConfiguration : IEntityTypeConfiguration<Funcionario>
{
    public void Configure(EntityTypeBuilder<Funcionario> builder)
    {
        builder.ToTable("funcionarios");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id_funcionario").ValueGeneratedNever();
        builder.Property(f => f.IdTenant).HasColumnName("id_tenant");
        builder.Property(f => f.Nome).HasMaxLength(150);
        builder.Property(f => f.Cpf).HasMaxLength(11).IsFixedLength();
        builder.Property(f => f.Cargo).HasMaxLength(100);
        builder.Property(f => f.Telefone).HasMaxLength(20);

        builder.HasOne<Usuario>().WithMany().HasForeignKey(f => f.IdUsuario).OnDelete(DeleteBehavior.Cascade);

        // CPF único na funerária e um cadastro de funcionário por pessoa e funerária (AUT-07).
        builder.HasIndex(f => new { f.IdTenant, f.Cpf }).IsUnique();
        builder.HasIndex(f => new { f.IdTenant, f.IdUsuario }).IsUnique();
    }
}

internal sealed class FuncionarioRepository(IdentidadeDbContext context) : IFuncionarioRepository
{
    public Task<bool> ExisteCpfAsync(Guid idTenant, string cpf, CancellationToken cancellationToken = default) =>
        context.Set<Funcionario>().AnyAsync(f => f.IdTenant == idTenant && f.Cpf == cpf, cancellationToken);

    public void Adicionar(Funcionario funcionario) => context.Set<Funcionario>().Add(funcionario);
}
