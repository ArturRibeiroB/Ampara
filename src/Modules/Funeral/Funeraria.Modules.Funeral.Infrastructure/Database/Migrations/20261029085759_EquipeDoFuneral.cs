using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Funeral.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class EquipeDoFuneral : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "funerais_equipe",
                schema: "funeral",
                columns: table => new
                {
                    id_membro = table.Column<Guid>(type: "uuid", nullable: false),
                    id_usuario = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    funcao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    atribuido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    id_funeral = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_funerais_equipe", x => x.id_membro);
                    table.ForeignKey(
                        name: "fk_funerais_equipe_funerais_id_funeral",
                        column: x => x.id_funeral,
                        principalSchema: "funeral",
                        principalTable: "funerais",
                        principalColumn: "id_funeral",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_funerais_equipe_id_funeral_id_usuario",
                schema: "funeral",
                table: "funerais_equipe",
                columns: new[] { "id_funeral", "id_usuario" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_funerais_equipe_id_usuario",
                schema: "funeral",
                table: "funerais_equipe",
                column: "id_usuario");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "funerais_equipe",
                schema: "funeral");
        }
    }
}
