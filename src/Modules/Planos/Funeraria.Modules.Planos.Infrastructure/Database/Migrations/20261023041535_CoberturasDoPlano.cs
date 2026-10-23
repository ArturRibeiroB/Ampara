using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Planos.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class CoberturasDoPlano : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "coberturas",
                schema: "planos",
                columns: table => new
                {
                    id_plano_cobertura = table.Column<Guid>(type: "uuid", nullable: false),
                    id_item_catalogo = table.Column<Guid>(type: "uuid", nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    km_translado_incluso = table.Column<int>(type: "integer", nullable: true),
                    id_plano = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_coberturas", x => x.id_plano_cobertura);
                    table.ForeignKey(
                        name: "fk_coberturas_planos_id_plano",
                        column: x => x.id_plano,
                        principalSchema: "planos",
                        principalTable: "planos",
                        principalColumn: "id_plano",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_coberturas_id_plano_id_item_catalogo",
                schema: "planos",
                table: "coberturas",
                columns: new[] { "id_plano", "id_item_catalogo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "coberturas",
                schema: "planos");
        }
    }
}
