using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Notificacoes.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class WhatsApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "consentimentos_whatsapp",
                schema: "notificacoes",
                columns: table => new
                {
                    id_consentimento = table.Column<Guid>(type: "uuid", nullable: false),
                    id_tenant = table.Column<Guid>(type: "uuid", nullable: false),
                    id_usuario = table.Column<Guid>(type: "uuid", nullable: false),
                    telefone = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    aceito_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    revogado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consentimentos_whatsapp", x => x.id_consentimento);
                });

            migrationBuilder.CreateTable(
                name: "mensagens_whatsapp",
                schema: "notificacoes",
                columns: table => new
                {
                    id_mensagem = table.Column<Guid>(type: "uuid", nullable: false),
                    id_tenant = table.Column<Guid>(type: "uuid", nullable: false),
                    id_usuario = table.Column<Guid>(type: "uuid", nullable: false),
                    modelo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    telefone = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    parametros = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    id_externo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    erro = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    criada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mensagens_whatsapp", x => x.id_mensagem);
                });

            migrationBuilder.CreateIndex(
                name: "ix_consentimentos_whatsapp_id_tenant",
                schema: "notificacoes",
                table: "consentimentos_whatsapp",
                column: "id_tenant");

            migrationBuilder.CreateIndex(
                name: "ix_consentimentos_whatsapp_tenant_id_id_usuario",
                schema: "notificacoes",
                table: "consentimentos_whatsapp",
                columns: new[] { "id_tenant", "id_usuario" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mensagens_whatsapp_id_externo",
                schema: "notificacoes",
                table: "mensagens_whatsapp",
                column: "id_externo");

            migrationBuilder.CreateIndex(
                name: "ix_mensagens_whatsapp_id_tenant",
                schema: "notificacoes",
                table: "mensagens_whatsapp",
                column: "id_tenant");

            migrationBuilder.CreateIndex(
                name: "ix_mensagens_whatsapp_tenant_id_id_usuario_criada_em",
                schema: "notificacoes",
                table: "mensagens_whatsapp",
                columns: new[] { "id_tenant", "id_usuario", "criada_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consentimentos_whatsapp",
                schema: "notificacoes");

            migrationBuilder.DropTable(
                name: "mensagens_whatsapp",
                schema: "notificacoes");
        }
    }
}
