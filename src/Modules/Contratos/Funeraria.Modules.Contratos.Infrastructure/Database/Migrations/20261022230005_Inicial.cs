using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Contratos.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "contratos");

            migrationBuilder.CreateTable(
                name: "contratos",
                schema: "contratos",
                columns: table => new
                {
                    id_contrato = table.Column<Guid>(type: "uuid", nullable: false),
                    id_tenant = table.Column<Guid>(type: "uuid", nullable: false),
                    id_cliente = table.Column<Guid>(type: "uuid", nullable: false),
                    id_plano = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_plano = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tipo_cobertura = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    periodicidade = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    dia_vencimento = table.Column<int>(type: "integer", nullable: false),
                    data_contratacao = table.Column<DateOnly>(type: "date", nullable: false),
                    data_fim = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    valor_plano = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    valor_por_dependente = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    valor_total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    carencia_dias = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contratos", x => x.id_contrato);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "contratos",
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
                name: "pessoas_cobertas",
                schema: "contratos",
                columns: table => new
                {
                    id_pessoa_coberta = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    id_dependente = table.Column<Guid>(type: "uuid", nullable: true),
                    data_inclusao = table.Column<DateOnly>(type: "date", nullable: false),
                    data_remocao = table.Column<DateOnly>(type: "date", nullable: true),
                    id_contrato = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pessoas_cobertas", x => x.id_pessoa_coberta);
                    table.ForeignKey(
                        name: "fk_pessoas_cobertas_contratos_id_contrato",
                        column: x => x.id_contrato,
                        principalSchema: "contratos",
                        principalTable: "contratos",
                        principalColumn: "id_contrato",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_contratos_id_tenant",
                schema: "contratos",
                table: "contratos",
                column: "id_tenant");

            migrationBuilder.CreateIndex(
                name: "ix_contratos_tenant_id_id_cliente_id_plano",
                schema: "contratos",
                table: "contratos",
                columns: new[] { "id_tenant", "id_cliente", "id_plano" },
                unique: true,
                filter: "status <> 'Cancelado'");

            migrationBuilder.CreateIndex(
                name: "ix_contratos_tenant_id_status",
                schema: "contratos",
                table: "contratos",
                columns: new[] { "id_tenant", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_occurred_on_utc",
                schema: "contratos",
                table: "outbox_messages",
                column: "occurred_on_utc",
                filter: "processed_on_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_pessoas_cobertas_id_contrato",
                schema: "contratos",
                table: "pessoas_cobertas",
                column: "id_contrato");

            migrationBuilder.CreateIndex(
                name: "ix_pessoas_cobertas_id_dependente",
                schema: "contratos",
                table: "pessoas_cobertas",
                column: "id_dependente",
                unique: true,
                filter: "id_dependente IS NOT NULL AND data_remocao IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "contratos");

            migrationBuilder.DropTable(
                name: "pessoas_cobertas",
                schema: "contratos");

            migrationBuilder.DropTable(
                name: "contratos",
                schema: "contratos");
        }
    }
}
