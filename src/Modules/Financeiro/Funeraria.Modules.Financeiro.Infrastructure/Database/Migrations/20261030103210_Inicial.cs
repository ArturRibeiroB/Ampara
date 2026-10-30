using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Financeiro.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "financeiro");

            migrationBuilder.CreateTable(
                name: "assinaturas",
                schema: "financeiro",
                columns: table => new
                {
                    id_assinatura = table.Column<Guid>(type: "uuid", nullable: false),
                    id_tenant = table.Column<Guid>(type: "uuid", nullable: false),
                    id_contrato = table.Column<Guid>(type: "uuid", nullable: false),
                    id_cliente = table.Column<Guid>(type: "uuid", nullable: false),
                    id_plano = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_plano = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    periodicidade = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    dia_vencimento = table.Column<int>(type: "integer", nullable: false),
                    data_contratacao = table.Column<DateOnly>(type: "date", nullable: false),
                    valor_plano = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    valor_por_dependente = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    quantidade_dependentes = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    id_stripe_customer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    id_stripe_subscription = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    id_item_plano = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    id_item_dependentes = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    inadimplente = table.Column<bool>(type: "boolean", nullable: false),
                    inadimplente_desde = table.Column<DateOnly>(type: "date", nullable: true),
                    ultimo_reajuste_em = table.Column<DateOnly>(type: "date", nullable: true),
                    aviso_reajuste_para = table.Column<DateOnly>(type: "date", nullable: true),
                    vigente_ate = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assinaturas", x => x.id_assinatura);
                });

            migrationBuilder.CreateTable(
                name: "clientes_stripe",
                schema: "financeiro",
                columns: table => new
                {
                    id_cliente_stripe = table.Column<Guid>(type: "uuid", nullable: false),
                    id_tenant = table.Column<Guid>(type: "uuid", nullable: false),
                    id_cliente = table.Column<Guid>(type: "uuid", nullable: false),
                    id_stripe_customer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clientes_stripe", x => x.id_cliente_stripe);
                });

            migrationBuilder.CreateTable(
                name: "contas_pagamento",
                schema: "financeiro",
                columns: table => new
                {
                    id_conta_pagamento = table.Column<Guid>(type: "uuid", nullable: false),
                    id_tenant = table.Column<Guid>(type: "uuid", nullable: false),
                    id_stripe_account = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    cobrancas_habilitadas = table.Column<bool>(type: "boolean", nullable: false),
                    cadastro_enviado = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contas_pagamento", x => x.id_conta_pagamento);
                });

            migrationBuilder.CreateTable(
                name: "eventos_stripe",
                schema: "financeiro",
                columns: table => new
                {
                    id_evento_stripe = table.Column<Guid>(type: "uuid", nullable: false),
                    id_evento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tipo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    id_stripe_account = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    conteudo = table.Column<string>(type: "text", nullable: false),
                    recebido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_eventos_stripe", x => x.id_evento_stripe);
                });

            migrationBuilder.CreateTable(
                name: "faturas",
                schema: "financeiro",
                columns: table => new
                {
                    id_fatura = table.Column<Guid>(type: "uuid", nullable: false),
                    id_tenant = table.Column<Guid>(type: "uuid", nullable: false),
                    id_contrato = table.Column<Guid>(type: "uuid", nullable: false),
                    id_cliente = table.Column<Guid>(type: "uuid", nullable: false),
                    id_stripe_invoice = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    competencia = table.Column<DateOnly>(type: "date", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    valor_multa_juros = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    data_vencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    forma_pagamento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    link_pagamento = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    data_pagamento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    processando_desde = table.Column<DateOnly>(type: "date", nullable: true),
                    avulsa = table.Column<bool>(type: "boolean", nullable: false),
                    observacao_baixa = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_faturas", x => x.id_fatura);
                });

            migrationBuilder.CreateTable(
                name: "faturas_lembretes",
                schema: "financeiro",
                columns: table => new
                {
                    id_lembrete = table.Column<Guid>(type: "uuid", nullable: false),
                    id_tenant = table.Column<Guid>(type: "uuid", nullable: false),
                    id_fatura = table.Column<Guid>(type: "uuid", nullable: false),
                    dias_do_vencimento = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_faturas_lembretes", x => x.id_lembrete);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "financeiro",
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
                name: "assinaturas_historico_valores",
                schema: "financeiro",
                columns: table => new
                {
                    id_historico = table.Column<Guid>(type: "uuid", nullable: false),
                    vigente_desde = table.Column<DateOnly>(type: "date", nullable: false),
                    valor_plano = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    valor_por_dependente = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    motivo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    id_assinatura = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assinaturas_historico_valores", x => x.id_historico);
                    table.ForeignKey(
                        name: "fk_assinaturas_historico_valores_assinaturas_id_assinatura",
                        column: x => x.id_assinatura,
                        principalSchema: "financeiro",
                        principalTable: "assinaturas",
                        principalColumn: "id_assinatura",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assinaturas_id_contrato",
                schema: "financeiro",
                table: "assinaturas",
                column: "id_contrato",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_assinaturas_id_stripe_subscription",
                schema: "financeiro",
                table: "assinaturas",
                column: "id_stripe_subscription",
                unique: true,
                filter: "id_stripe_subscription IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_assinaturas_id_tenant",
                schema: "financeiro",
                table: "assinaturas",
                column: "id_tenant");

            migrationBuilder.CreateIndex(
                name: "ix_assinaturas_historico_valores_id_assinatura",
                schema: "financeiro",
                table: "assinaturas_historico_valores",
                column: "id_assinatura");

            migrationBuilder.CreateIndex(
                name: "ix_clientes_stripe_id_tenant",
                schema: "financeiro",
                table: "clientes_stripe",
                column: "id_tenant");

            migrationBuilder.CreateIndex(
                name: "ix_clientes_stripe_tenant_id_id_cliente",
                schema: "financeiro",
                table: "clientes_stripe",
                columns: new[] { "id_tenant", "id_cliente" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_contas_pagamento_id_stripe_account",
                schema: "financeiro",
                table: "contas_pagamento",
                column: "id_stripe_account",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_contas_pagamento_tenant_id",
                schema: "financeiro",
                table: "contas_pagamento",
                column: "id_tenant",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_eventos_stripe_id_evento",
                schema: "financeiro",
                table: "eventos_stripe",
                column: "id_evento",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_faturas_id_contrato_data_vencimento",
                schema: "financeiro",
                table: "faturas",
                columns: new[] { "id_contrato", "data_vencimento" });

            migrationBuilder.CreateIndex(
                name: "ix_faturas_id_stripe_invoice",
                schema: "financeiro",
                table: "faturas",
                column: "id_stripe_invoice",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_faturas_id_tenant",
                schema: "financeiro",
                table: "faturas",
                column: "id_tenant");

            migrationBuilder.CreateIndex(
                name: "ix_faturas_tenant_id_status_data_vencimento",
                schema: "financeiro",
                table: "faturas",
                columns: new[] { "id_tenant", "status", "data_vencimento" });

            migrationBuilder.CreateIndex(
                name: "ix_faturas_lembretes_id_fatura_dias_do_vencimento",
                schema: "financeiro",
                table: "faturas_lembretes",
                columns: new[] { "id_fatura", "dias_do_vencimento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_faturas_lembretes_id_tenant",
                schema: "financeiro",
                table: "faturas_lembretes",
                column: "id_tenant");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_occurred_on_utc",
                schema: "financeiro",
                table: "outbox_messages",
                column: "occurred_on_utc",
                filter: "processed_on_utc IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assinaturas_historico_valores",
                schema: "financeiro");

            migrationBuilder.DropTable(
                name: "clientes_stripe",
                schema: "financeiro");

            migrationBuilder.DropTable(
                name: "contas_pagamento",
                schema: "financeiro");

            migrationBuilder.DropTable(
                name: "eventos_stripe",
                schema: "financeiro");

            migrationBuilder.DropTable(
                name: "faturas",
                schema: "financeiro");

            migrationBuilder.DropTable(
                name: "faturas_lembretes",
                schema: "financeiro");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "financeiro");

            migrationBuilder.DropTable(
                name: "assinaturas",
                schema: "financeiro");
        }
    }
}
