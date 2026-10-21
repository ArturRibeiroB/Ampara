using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Identidade.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class TokensSenha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tokens_senha",
                schema: "identidade",
                columns: table => new
                {
                    id_token_senha = table.Column<Guid>(type: "uuid", nullable: false),
                    id_usuario = table.Column<Guid>(type: "uuid", nullable: false),
                    finalidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    token_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expira_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tokens_senha", x => x.id_token_senha);
                    table.ForeignKey(
                        name: "fk_tokens_senha_usuarios_id_usuario",
                        column: x => x.id_usuario,
                        principalSchema: "identidade",
                        principalTable: "usuarios",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tokens_senha_id_usuario",
                schema: "identidade",
                table: "tokens_senha",
                column: "id_usuario");

            migrationBuilder.CreateIndex(
                name: "ix_tokens_senha_token_hash",
                schema: "identidade",
                table: "tokens_senha",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tokens_senha",
                schema: "identidade");
        }
    }
}
