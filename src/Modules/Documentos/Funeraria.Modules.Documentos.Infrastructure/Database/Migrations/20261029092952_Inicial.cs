using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Documentos.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "documentos");

            migrationBuilder.CreateTable(
                name: "documentos",
                schema: "documentos",
                columns: table => new
                {
                    id_documento = table.Column<Guid>(type: "uuid", nullable: false),
                    id_tenant = table.Column<Guid>(type: "uuid", nullable: false),
                    id_beneficiario = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    nome_original = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    chave = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tamanho = table.Column<long>(type: "bigint", nullable: false),
                    enviado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documentos", x => x.id_documento);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "documentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    content = table.Column<string>(type: "jsonb", nullable: false),
                    id_tenant = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_documentos_id_beneficiario",
                schema: "documentos",
                table: "documentos",
                column: "id_beneficiario");

            migrationBuilder.CreateIndex(
                name: "ix_documentos_id_tenant",
                schema: "documentos",
                table: "documentos",
                column: "id_tenant");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_occurred_on_utc",
                schema: "documentos",
                table: "outbox_messages",
                column: "occurred_on_utc",
                filter: "processed_on_utc IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "documentos",
                schema: "documentos");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "documentos");
        }
    }
}
