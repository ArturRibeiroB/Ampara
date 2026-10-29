using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Funeral.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class BeneficiariosEFunerais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "beneficiarios",
                schema: "funeral",
                columns: table => new
                {
                    id_beneficiario = table.Column<Guid>(type: "uuid", nullable: false),
                    id_tenant = table.Column<Guid>(type: "uuid", nullable: false),
                    id_pessoa_coberta = table.Column<Guid>(type: "uuid", nullable: false),
                    id_contrato = table.Column<Guid>(type: "uuid", nullable: false),
                    id_cliente = table.Column<Guid>(type: "uuid", nullable: false),
                    id_dependente = table.Column<Guid>(type: "uuid", nullable: true),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    data_nascimento = table.Column<DateOnly>(type: "date", nullable: false),
                    data_falecimento = table.Column<DateOnly>(type: "date", nullable: false),
                    local_falecimento = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    responsavel_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    responsavel_parentesco = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    responsavel_telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_beneficiarios", x => x.id_beneficiario);
                });

            migrationBuilder.CreateTable(
                name: "funerais",
                schema: "funeral",
                columns: table => new
                {
                    id_funeral = table.Column<Guid>(type: "uuid", nullable: false),
                    id_tenant = table.Column<Guid>(type: "uuid", nullable: false),
                    id_beneficiario = table.Column<Guid>(type: "uuid", nullable: false),
                    id_contrato = table.Column<Guid>(type: "uuid", nullable: false),
                    id_cliente = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_falecido = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    data_hora_prevista = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    destinacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    observacoes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_funerais", x => x.id_funeral);
                    table.ForeignKey(
                        name: "fk_funerais_beneficiarios_id_beneficiario",
                        column: x => x.id_beneficiario,
                        principalSchema: "funeral",
                        principalTable: "beneficiarios",
                        principalColumn: "id_beneficiario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "funerais_historico",
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
                    id_funeral = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_funerais_historico", x => x.id_historico);
                    table.ForeignKey(
                        name: "fk_funerais_historico_funerais_id_funeral",
                        column: x => x.id_funeral,
                        principalSchema: "funeral",
                        principalTable: "funerais",
                        principalColumn: "id_funeral",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_beneficiarios_id_cliente",
                schema: "funeral",
                table: "beneficiarios",
                column: "id_cliente");

            migrationBuilder.CreateIndex(
                name: "ix_beneficiarios_id_pessoa_coberta",
                schema: "funeral",
                table: "beneficiarios",
                column: "id_pessoa_coberta",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_beneficiarios_id_tenant",
                schema: "funeral",
                table: "beneficiarios",
                column: "id_tenant");

            migrationBuilder.CreateIndex(
                name: "ix_funerais_id_beneficiario",
                schema: "funeral",
                table: "funerais",
                column: "id_beneficiario",
                unique: true,
                filter: "status <> 'Cancelado'");

            migrationBuilder.CreateIndex(
                name: "ix_funerais_id_contrato",
                schema: "funeral",
                table: "funerais",
                column: "id_contrato");

            migrationBuilder.CreateIndex(
                name: "ix_funerais_id_tenant",
                schema: "funeral",
                table: "funerais",
                column: "id_tenant");

            migrationBuilder.CreateIndex(
                name: "ix_funerais_tenant_id_status_data_hora_prevista",
                schema: "funeral",
                table: "funerais",
                columns: new[] { "id_tenant", "status", "data_hora_prevista" });

            migrationBuilder.CreateIndex(
                name: "ix_funerais_historico_id_funeral",
                schema: "funeral",
                table: "funerais_historico",
                column: "id_funeral");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "funerais_historico",
                schema: "funeral");

            migrationBuilder.DropTable(
                name: "funerais",
                schema: "funeral");

            migrationBuilder.DropTable(
                name: "beneficiarios",
                schema: "funeral");
        }
    }
}
