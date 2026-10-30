using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Clientes.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class CpfCifrado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_dependentes_id_cliente_cpf",
                schema: "clientes",
                table: "dependentes");

            migrationBuilder.DropIndex(
                name: "ix_clientes_tenant_id_cpf",
                schema: "clientes",
                table: "clientes");

            migrationBuilder.AlterColumn<string>(
                name: "cpf",
                schema: "clientes",
                table: "dependentes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character(11)",
                oldFixedLength: true,
                oldMaxLength: 11);

            migrationBuilder.AddColumn<string>(
                name: "cpf_hash",
                schema: "clientes",
                table: "dependentes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "cpf",
                schema: "clientes",
                table: "clientes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character(11)",
                oldFixedLength: true,
                oldMaxLength: 11);

            migrationBuilder.AddColumn<string>(
                name: "cpf_hash",
                schema: "clientes",
                table: "clientes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_dependentes_id_cliente_cpf_hash",
                schema: "clientes",
                table: "dependentes",
                columns: new[] { "id_cliente", "cpf_hash" },
                unique: true,
                filter: "removido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_clientes_tenant_id_cpf_hash",
                schema: "clientes",
                table: "clientes",
                columns: new[] { "id_tenant", "cpf_hash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_dependentes_id_cliente_cpf_hash",
                schema: "clientes",
                table: "dependentes");

            migrationBuilder.DropIndex(
                name: "ix_clientes_tenant_id_cpf_hash",
                schema: "clientes",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "cpf_hash",
                schema: "clientes",
                table: "dependentes");

            migrationBuilder.DropColumn(
                name: "cpf_hash",
                schema: "clientes",
                table: "clientes");

            migrationBuilder.AlterColumn<string>(
                name: "cpf",
                schema: "clientes",
                table: "dependentes",
                type: "character(11)",
                fixedLength: true,
                maxLength: 11,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "cpf",
                schema: "clientes",
                table: "clientes",
                type: "character(11)",
                fixedLength: true,
                maxLength: 11,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.CreateIndex(
                name: "ix_dependentes_id_cliente_cpf",
                schema: "clientes",
                table: "dependentes",
                columns: new[] { "id_cliente", "cpf" },
                unique: true,
                filter: "removido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_clientes_tenant_id_cpf",
                schema: "clientes",
                table: "clientes",
                columns: new[] { "id_tenant", "cpf" },
                unique: true);
        }
    }
}
