using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Translado.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "translado");

            migrationBuilder.CreateTable(
                name: "configuracoes",
                schema: "translado",
                columns: table => new
                {
                    id_configuracao = table.Column<Guid>(type: "uuid", nullable: false),
                    id_tenant = table.Column<Guid>(type: "uuid", nullable: false),
                    faixas = table.Column<string>(type: "jsonb", nullable: false),
                    documentos_exigidos = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracoes", x => x.id_configuracao);
                });

            migrationBuilder.CreateTable(
                name: "distancias_calculadas",
                schema: "translado",
                columns: table => new
                {
                    id_distancia = table.Column<Guid>(type: "uuid", nullable: false),
                    chave = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    km = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_distancias_calculadas", x => x.id_distancia);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "translado",
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
                name: "translados",
                schema: "translado",
                columns: table => new
                {
                    id_translado = table.Column<Guid>(type: "uuid", nullable: false),
                    id_tenant = table.Column<Guid>(type: "uuid", nullable: false),
                    id_funeral = table.Column<Guid>(type: "uuid", nullable: false),
                    id_beneficiario = table.Column<Guid>(type: "uuid", nullable: false),
                    id_cliente = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_falecido = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    contato_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    contato_telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    data_prevista = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    distancia_km = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    distancia_manual = table.Column<bool>(type: "boolean", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    valor_coberto_plano = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    id_terceiro_frota = table.Column<Guid>(type: "uuid", nullable: true),
                    nome_frota = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    id_atribuicao = table.Column<Guid>(type: "uuid", nullable: true),
                    status_aceite = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    motivo_recusa = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    motorista_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    motorista_telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    veiculo_placa = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    veiculo_modelo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    destino_cidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    destino_endereco = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    destino_pais = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    destino_uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    origem_cidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    origem_endereco = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    origem_pais = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    origem_uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_translados", x => x.id_translado);
                });

            migrationBuilder.CreateTable(
                name: "translados_historico",
                schema: "translado",
                columns: table => new
                {
                    id_historico = table.Column<Guid>(type: "uuid", nullable: false),
                    de = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    para = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    acao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    id_usuario = table.Column<Guid>(type: "uuid", nullable: true),
                    observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    id_translado = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_translados_historico", x => x.id_historico);
                    table.ForeignKey(
                        name: "fk_translados_historico_translados_id_translado",
                        column: x => x.id_translado,
                        principalSchema: "translado",
                        principalTable: "translados",
                        principalColumn: "id_translado",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_configuracoes_tenant_id",
                schema: "translado",
                table: "configuracoes",
                column: "id_tenant",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_distancias_calculadas_chave",
                schema: "translado",
                table: "distancias_calculadas",
                column: "chave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_occurred_on_utc",
                schema: "translado",
                table: "outbox_messages",
                column: "occurred_on_utc",
                filter: "processed_on_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_translados_id_funeral",
                schema: "translado",
                table: "translados",
                column: "id_funeral");

            migrationBuilder.CreateIndex(
                name: "ix_translados_id_tenant",
                schema: "translado",
                table: "translados",
                column: "id_tenant");

            migrationBuilder.CreateIndex(
                name: "ix_translados_id_terceiro_frota",
                schema: "translado",
                table: "translados",
                column: "id_terceiro_frota");

            migrationBuilder.CreateIndex(
                name: "ix_translados_historico_id_translado",
                schema: "translado",
                table: "translados_historico",
                column: "id_translado");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "configuracoes",
                schema: "translado");

            migrationBuilder.DropTable(
                name: "distancias_calculadas",
                schema: "translado");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "translado");

            migrationBuilder.DropTable(
                name: "translados_historico",
                schema: "translado");

            migrationBuilder.DropTable(
                name: "translados",
                schema: "translado");
        }
    }
}
