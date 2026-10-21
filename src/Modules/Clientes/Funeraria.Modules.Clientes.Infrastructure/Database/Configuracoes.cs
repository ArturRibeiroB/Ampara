using Funeraria.Modules.Clientes.Domain.Clientes;
using Funeraria.Modules.Clientes.Domain.Dependentes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Funeraria.Modules.Clientes.Infrastructure.Database;

internal sealed class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("clientes");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id_cliente").ValueGeneratedNever();

        builder.Property(c => c.Nome).HasMaxLength(150);
        builder.Property(c => c.Cpf).HasMaxLength(11).IsFixedLength();
        builder.Property(c => c.Email).HasMaxLength(254);
        builder.Property(c => c.Telefone).HasMaxLength(20);
        builder.Property(c => c.Endereco).HasMaxLength(200);
        builder.Property(c => c.Cep).HasMaxLength(8).IsFixedLength();
        builder.Property(c => c.Cidade).HasMaxLength(100);
        builder.Property(c => c.Estado).HasMaxLength(2).IsFixedLength();

        // CPF único dentro da funerária: a mesma pessoa pode ser cliente de várias funerárias (DEC-02, TEN-01).
        builder.HasIndex(c => new { c.TenantId, c.Cpf }).IsUnique();
        builder.HasIndex(c => c.IdUsuario);
    }
}

internal sealed class DependenteConfiguration : IEntityTypeConfiguration<Dependente>
{
    public void Configure(EntityTypeBuilder<Dependente> builder)
    {
        builder.ToTable("dependentes");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id_dependente").ValueGeneratedNever();

        builder.Property(d => d.Nome).HasMaxLength(150);
        builder.Property(d => d.Cpf).HasMaxLength(11).IsFixedLength();
        builder.Property(d => d.Parentesco).HasMaxLength(50);
        builder.Property(d => d.Telefone).HasMaxLength(20);
        builder.Property(d => d.Email).HasMaxLength(254);

        builder.HasOne<Cliente>().WithMany().HasForeignKey(d => d.IdCliente).OnDelete(DeleteBehavior.Restrict);

        // O mesmo dependente não entra duas vezes para o mesmo titular; removidos não contam (soft delete).
        builder.HasIndex(d => new { d.IdCliente, d.Cpf }).IsUnique().HasFilter("removido_em IS NULL");
    }
}
