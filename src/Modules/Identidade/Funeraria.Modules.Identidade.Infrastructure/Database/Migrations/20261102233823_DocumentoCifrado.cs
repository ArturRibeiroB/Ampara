using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Identidade.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class DocumentoCifrado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_funcionarios_id_tenant_cpf",
                schema: "identidade",
                table: "funcionarios");

            migrationBuilder.AlterColumn<string>(
                name: "cpf",
                schema: "identidade",
                table: "funcionarios",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character(11)",
                oldFixedLength: true,
                oldMaxLength: 11);

            migrationBuilder.AddColumn<string>(
                name: "cpf_hash",
                schema: "identidade",
                table: "funcionarios",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_funcionarios_id_tenant_cpf_hash",
                schema: "identidade",
                table: "funcionarios",
                columns: new[] { "id_tenant", "cpf_hash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_funcionarios_id_tenant_cpf_hash",
                schema: "identidade",
                table: "funcionarios");

            migrationBuilder.DropColumn(
                name: "cpf_hash",
                schema: "identidade",
                table: "funcionarios");

            migrationBuilder.AlterColumn<string>(
                name: "cpf",
                schema: "identidade",
                table: "funcionarios",
                type: "character(11)",
                fixedLength: true,
                maxLength: 11,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.CreateIndex(
                name: "ix_funcionarios_id_tenant_cpf",
                schema: "identidade",
                table: "funcionarios",
                columns: new[] { "id_tenant", "cpf" },
                unique: true);
        }
    }
}
