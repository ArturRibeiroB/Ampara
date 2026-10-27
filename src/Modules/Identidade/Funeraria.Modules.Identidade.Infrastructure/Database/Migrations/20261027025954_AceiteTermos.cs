using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Identidade.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AceiteTermos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "aceites_termos",
                schema: "identidade",
                columns: table => new
                {
                    id_aceite = table.Column<Guid>(type: "uuid", nullable: false),
                    id_usuario = table.Column<Guid>(type: "uuid", nullable: false),
                    documento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    versao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    aceito_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_aceites_termos", x => x.id_aceite);
                    table.ForeignKey(
                        name: "fk_aceites_termos_usuarios_id_usuario",
                        column: x => x.id_usuario,
                        principalSchema: "identidade",
                        principalTable: "usuarios",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_aceites_termos_id_usuario",
                schema: "identidade",
                table: "aceites_termos",
                column: "id_usuario");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aceites_termos",
                schema: "identidade");
        }
    }
}
