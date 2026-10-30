using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Funeral.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "funeral");

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "funeral",
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

            migrationBuilder.CreateTable(
                name: "preferencias",
                schema: "funeral",
                columns: table => new
                {
                    id_preferencia = table.Column<Guid>(type: "uuid", nullable: false),
                    id_tenant = table.Column<Guid>(type: "uuid", nullable: false),
                    id_cliente = table.Column<Guid>(type: "uuid", nullable: false),
                    id_pessoa = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_pessoa = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    destinacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tipo_cerimonia = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    crenca_religiosa = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    musicas = table.Column<string>(type: "jsonb", nullable: false),
                    mensagens = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    observacoes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    itens = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_preferencias", x => x.id_preferencia);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_occurred_on_utc",
                schema: "funeral",
                table: "outbox_messages",
                column: "occurred_on_utc",
                filter: "processed_on_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_preferencias_id_cliente",
                schema: "funeral",
                table: "preferencias",
                column: "id_cliente");

            migrationBuilder.CreateIndex(
                name: "ix_preferencias_id_tenant",
                schema: "funeral",
                table: "preferencias",
                column: "id_tenant");

            migrationBuilder.CreateIndex(
                name: "ix_preferencias_tenant_id_id_pessoa",
                schema: "funeral",
                table: "preferencias",
                columns: new[] { "id_tenant", "id_pessoa" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "funeral");

            migrationBuilder.DropTable(
                name: "preferencias",
                schema: "funeral");
        }
    }
}
