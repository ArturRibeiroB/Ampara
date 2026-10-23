using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Terceiros.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Atribuicoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "atribuicoes",
                schema: "terceiros",
                columns: table => new
                {
                    id_atribuicao = table.Column<Guid>(type: "uuid", nullable: false),
                    id_tenant = table.Column<Guid>(type: "uuid", nullable: false),
                    id_terceiro = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    id_origem = table.Column<Guid>(type: "uuid", nullable: false),
                    resumo = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    prazo_aceite = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    respondida_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    motivo_recusa = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_atribuicoes", x => x.id_atribuicao);
                    table.ForeignKey(
                        name: "fk_atribuicoes_terceiros_id_terceiro",
                        column: x => x.id_terceiro,
                        principalSchema: "terceiros",
                        principalTable: "terceiros",
                        principalColumn: "id_terceiro",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_atribuicoes_id_tenant",
                schema: "terceiros",
                table: "atribuicoes",
                column: "id_tenant");

            migrationBuilder.CreateIndex(
                name: "ix_atribuicoes_id_terceiro_tipo_id_origem",
                schema: "terceiros",
                table: "atribuicoes",
                columns: new[] { "id_terceiro", "tipo", "id_origem" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "atribuicoes",
                schema: "terceiros");
        }
    }
}
