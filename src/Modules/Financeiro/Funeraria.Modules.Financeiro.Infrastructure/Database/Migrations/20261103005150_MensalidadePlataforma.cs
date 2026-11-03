using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Financeiro.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class MensalidadePlataforma : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "taxa_plataforma_percentual",
                schema: "financeiro",
                table: "contas_pagamento",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "configuracao_plataforma",
                schema: "financeiro",
                columns: table => new
                {
                    id_configuracao = table.Column<Guid>(type: "uuid", nullable: false),
                    valor_mensalidade = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    taxa_percentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracao_plataforma", x => x.id_configuracao);
                });

            migrationBuilder.CreateTable(
                name: "mensalidades_plataforma",
                schema: "financeiro",
                columns: table => new
                {
                    id_mensalidade = table.Column<Guid>(type: "uuid", nullable: false),
                    id_tenant = table.Column<Guid>(type: "uuid", nullable: false),
                    id_stripe_customer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    id_stripe_subscription = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    valor = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    id_ultima_fatura = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    valor_ultima_fatura = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    vencimento_ultima_fatura = table.Column<DateOnly>(type: "date", nullable: true),
                    situacao_ultima_fatura = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    link_pagamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ultimo_pagamento_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mensalidades_plataforma", x => x.id_mensalidade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mensalidades_plataforma_id_stripe_subscription",
                schema: "financeiro",
                table: "mensalidades_plataforma",
                column: "id_stripe_subscription",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mensalidades_plataforma_tenant_id",
                schema: "financeiro",
                table: "mensalidades_plataforma",
                column: "id_tenant",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "configuracao_plataforma",
                schema: "financeiro");

            migrationBuilder.DropTable(
                name: "mensalidades_plataforma",
                schema: "financeiro");

            migrationBuilder.DropColumn(
                name: "taxa_plataforma_percentual",
                schema: "financeiro",
                table: "contas_pagamento");
        }
    }
}
