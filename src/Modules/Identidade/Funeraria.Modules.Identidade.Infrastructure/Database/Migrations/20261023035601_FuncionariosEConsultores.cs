using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Identidade.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class FuncionariosEConsultores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "funcionarios",
                schema: "identidade",
                columns: table => new
                {
                    id_funcionario = table.Column<Guid>(type: "uuid", nullable: false),
                    id_tenant = table.Column<Guid>(type: "uuid", nullable: false),
                    id_usuario = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    cpf = table.Column<string>(type: "character(11)", fixedLength: true, maxLength: 11, nullable: false),
                    cargo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_funcionarios", x => x.id_funcionario);
                    table.ForeignKey(
                        name: "fk_funcionarios_usuarios_id_usuario",
                        column: x => x.id_usuario,
                        principalSchema: "identidade",
                        principalTable: "usuarios",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_funcionarios_id_tenant_cpf",
                schema: "identidade",
                table: "funcionarios",
                columns: new[] { "id_tenant", "cpf" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_funcionarios_id_tenant_id_usuario",
                schema: "identidade",
                table: "funcionarios",
                columns: new[] { "id_tenant", "id_usuario" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_funcionarios_id_usuario",
                schema: "identidade",
                table: "funcionarios",
                column: "id_usuario");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "funcionarios",
                schema: "identidade");
        }
    }
}
