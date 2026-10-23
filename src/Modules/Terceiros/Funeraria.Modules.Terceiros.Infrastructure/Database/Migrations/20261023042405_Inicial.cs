using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Terceiros.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "terceiros");

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "terceiros",
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
                name: "terceiros",
                schema: "terceiros",
                columns: table => new
                {
                    id_terceiro = table.Column<Guid>(type: "uuid", nullable: false),
                    id_tenant = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    nome_razao_social = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    cpf_cnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    area_atuacao = table.Column<string>(type: "jsonb", nullable: false),
                    categorias_atendidas = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    id_usuario = table.Column<Guid>(type: "uuid", nullable: true),
                    conta_ativa = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_terceiros", x => x.id_terceiro);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_occurred_on_utc",
                schema: "terceiros",
                table: "outbox_messages",
                column: "occurred_on_utc",
                filter: "processed_on_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_terceiros_id_tenant",
                schema: "terceiros",
                table: "terceiros",
                column: "id_tenant");

            migrationBuilder.CreateIndex(
                name: "ix_terceiros_id_usuario",
                schema: "terceiros",
                table: "terceiros",
                column: "id_usuario");

            migrationBuilder.CreateIndex(
                name: "ix_terceiros_tenant_id_cpf_cnpj",
                schema: "terceiros",
                table: "terceiros",
                columns: new[] { "id_tenant", "cpf_cnpj" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_terceiros_tenant_id_email",
                schema: "terceiros",
                table: "terceiros",
                columns: new[] { "id_tenant", "email" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "terceiros");

            migrationBuilder.DropTable(
                name: "terceiros",
                schema: "terceiros");
        }
    }
}
