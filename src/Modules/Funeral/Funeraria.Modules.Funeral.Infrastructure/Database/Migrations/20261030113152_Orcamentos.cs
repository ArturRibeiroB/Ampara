using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Funeral.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Orcamentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "orcamentos",
                schema: "funeral",
                columns: table => new
                {
                    id_orcamento = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    itens = table.Column<string>(type: "jsonb", nullable: false),
                    total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    gerado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    id_usuario_geracao = table.Column<Guid>(type: "uuid", nullable: true),
                    aprovado_por = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    aprovado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    id_usuario_aprovacao = table.Column<Guid>(type: "uuid", nullable: true),
                    id_fatura = table.Column<Guid>(type: "uuid", nullable: true),
                    link_pagamento = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    id_funeral = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orcamentos", x => x.id_orcamento);
                    table.ForeignKey(
                        name: "fk_orcamentos_funerais_id_funeral",
                        column: x => x.id_funeral,
                        principalSchema: "funeral",
                        principalTable: "funerais",
                        principalColumn: "id_funeral",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_orcamentos_id_funeral",
                schema: "funeral",
                table: "orcamentos",
                column: "id_funeral");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "orcamentos",
                schema: "funeral");
        }
    }
}
