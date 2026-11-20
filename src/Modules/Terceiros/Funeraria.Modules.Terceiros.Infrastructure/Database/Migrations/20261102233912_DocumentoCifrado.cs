using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Terceiros.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class DocumentoCifrado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_terceiros_tenant_id_cpf_cnpj",
                schema: "terceiros",
                table: "terceiros");

            migrationBuilder.AlterColumn<string>(
                name: "cpf_cnpj",
                schema: "terceiros",
                table: "terceiros",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(14)",
                oldMaxLength: 14);

            migrationBuilder.AddColumn<string>(
                name: "cpf_cnpj_hash",
                schema: "terceiros",
                table: "terceiros",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_terceiros_tenant_id_cpf_cnpj_hash",
                schema: "terceiros",
                table: "terceiros",
                columns: new[] { "id_tenant", "cpf_cnpj_hash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_terceiros_tenant_id_cpf_cnpj_hash",
                schema: "terceiros",
                table: "terceiros");

            migrationBuilder.DropColumn(
                name: "cpf_cnpj_hash",
                schema: "terceiros",
                table: "terceiros");

            migrationBuilder.AlterColumn<string>(
                name: "cpf_cnpj",
                schema: "terceiros",
                table: "terceiros",
                type: "character varying(14)",
                maxLength: 14,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.CreateIndex(
                name: "ix_terceiros_tenant_id_cpf_cnpj",
                schema: "terceiros",
                table: "terceiros",
                columns: new[] { "id_tenant", "cpf_cnpj" },
                unique: true);
        }
    }
}
