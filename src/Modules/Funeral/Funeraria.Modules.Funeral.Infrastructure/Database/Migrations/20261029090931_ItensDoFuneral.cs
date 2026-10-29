using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Funeral.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class ItensDoFuneral : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "funerais_itens",
                schema: "funeral",
                columns: table => new
                {
                    id_funeral_item = table.Column<Guid>(type: "uuid", nullable: false),
                    id_item_catalogo = table.Column<Guid>(type: "uuid", nullable: false),
                    id_variacao = table.Column<Guid>(type: "uuid", nullable: true),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    categoria = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    nome_variacao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    preco_base = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    acrescimo_variacao = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    id_terceiro_fornecedor = table.Column<Guid>(type: "uuid", nullable: true),
                    estoque_baixado = table.Column<bool>(type: "boolean", nullable: false),
                    origem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    incluido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    id_funeral = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_funerais_itens", x => x.id_funeral_item);
                    table.ForeignKey(
                        name: "fk_funerais_itens_funerais_id_funeral",
                        column: x => x.id_funeral,
                        principalSchema: "funeral",
                        principalTable: "funerais",
                        principalColumn: "id_funeral",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_funerais_itens_id_funeral",
                schema: "funeral",
                table: "funerais_itens",
                column: "id_funeral");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "funerais_itens",
                schema: "funeral");
        }
    }
}
