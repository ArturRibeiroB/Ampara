using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Funeral.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class PedidosAFornecedores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pedidos",
                schema: "funeral",
                columns: table => new
                {
                    id_pedido = table.Column<Guid>(type: "uuid", nullable: false),
                    id_tenant = table.Column<Guid>(type: "uuid", nullable: false),
                    id_funeral = table.Column<Guid>(type: "uuid", nullable: false),
                    id_terceiro = table.Column<Guid>(type: "uuid", nullable: false),
                    id_atribuicao = table.Column<Guid>(type: "uuid", nullable: true),
                    nome_falecido = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    itens = table.Column<string>(type: "jsonb", nullable: false),
                    local_entrega = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    data_hora_entrega = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    observacoes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    motivo_recusa = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pedidos", x => x.id_pedido);
                });

            migrationBuilder.CreateTable(
                name: "pedidos_historico",
                schema: "funeral",
                columns: table => new
                {
                    id_historico = table.Column<Guid>(type: "uuid", nullable: false),
                    de = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    para = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    acao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    id_usuario = table.Column<Guid>(type: "uuid", nullable: true),
                    observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    id_pedido = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pedidos_historico", x => x.id_historico);
                    table.ForeignKey(
                        name: "fk_pedidos_historico_pedidos_id_pedido",
                        column: x => x.id_pedido,
                        principalSchema: "funeral",
                        principalTable: "pedidos",
                        principalColumn: "id_pedido",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_id_funeral",
                schema: "funeral",
                table: "pedidos",
                column: "id_funeral");

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_id_tenant",
                schema: "funeral",
                table: "pedidos",
                column: "id_tenant");

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_id_terceiro_status",
                schema: "funeral",
                table: "pedidos",
                columns: new[] { "id_terceiro", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_historico_id_pedido",
                schema: "funeral",
                table: "pedidos_historico",
                column: "id_pedido");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pedidos_historico",
                schema: "funeral");

            migrationBuilder.DropTable(
                name: "pedidos",
                schema: "funeral");
        }
    }
}
