using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Catalogo.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalogo");

            migrationBuilder.CreateTable(
                name: "categorias",
                schema: "catalogo",
                columns: table => new
                {
                    id_categoria = table.Column<Guid>(type: "uuid", nullable: false),
                    id_tenant = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categorias", x => x.id_categoria);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "catalogo",
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
                name: "itens",
                schema: "catalogo",
                columns: table => new
                {
                    id_item_catalogo = table.Column<Guid>(type: "uuid", nullable: false),
                    id_tenant = table.Column<Guid>(type: "uuid", nullable: false),
                    id_categoria = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    preco = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    controla_estoque = table.Column<bool>(type: "boolean", nullable: false),
                    quantidade_estoque = table.Column<int>(type: "integer", nullable: true),
                    imagem_chave = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    imagem_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_itens", x => x.id_item_catalogo);
                    table.ForeignKey(
                        name: "fk_itens_categorias_id_categoria",
                        column: x => x.id_categoria,
                        principalSchema: "catalogo",
                        principalTable: "categorias",
                        principalColumn: "id_categoria",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "variacoes",
                schema: "catalogo",
                columns: table => new
                {
                    id_variacao = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    acrescimo_preco = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    id_item_catalogo = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_variacoes", x => x.id_variacao);
                    table.ForeignKey(
                        name: "fk_variacoes_itens_id_item_catalogo",
                        column: x => x.id_item_catalogo,
                        principalSchema: "catalogo",
                        principalTable: "itens",
                        principalColumn: "id_item_catalogo",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_categorias_id_tenant",
                schema: "catalogo",
                table: "categorias",
                column: "id_tenant");

            migrationBuilder.CreateIndex(
                name: "ix_categorias_tenant_id_nome",
                schema: "catalogo",
                table: "categorias",
                columns: new[] { "id_tenant", "nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_itens_id_categoria",
                schema: "catalogo",
                table: "itens",
                column: "id_categoria");

            migrationBuilder.CreateIndex(
                name: "ix_itens_id_tenant",
                schema: "catalogo",
                table: "itens",
                column: "id_tenant");

            migrationBuilder.CreateIndex(
                name: "ix_itens_tenant_id_id_categoria_nome",
                schema: "catalogo",
                table: "itens",
                columns: new[] { "id_tenant", "id_categoria", "nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_occurred_on_utc",
                schema: "catalogo",
                table: "outbox_messages",
                column: "occurred_on_utc",
                filter: "processed_on_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_variacoes_id_item_catalogo",
                schema: "catalogo",
                table: "variacoes",
                column: "id_item_catalogo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "catalogo");

            migrationBuilder.DropTable(
                name: "variacoes",
                schema: "catalogo");

            migrationBuilder.DropTable(
                name: "itens",
                schema: "catalogo");

            migrationBuilder.DropTable(
                name: "categorias",
                schema: "catalogo");
        }
    }
}
