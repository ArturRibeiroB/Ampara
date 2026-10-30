using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Documentos.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Certidoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "solicitacoes_certidao",
                schema: "documentos",
                columns: table => new
                {
                    id_solicitacao = table.Column<Guid>(type: "uuid", nullable: false),
                    id_tenant = table.Column<Guid>(type: "uuid", nullable: false),
                    id_beneficiario = table.Column<Guid>(type: "uuid", nullable: false),
                    id_cliente = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_falecido = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    modo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    prazo_registro = table.Column<DateOnly>(type: "date", nullable: false),
                    tipo_do_responsavel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    id_usuario_responsavel = table.Column<Guid>(type: "uuid", nullable: true),
                    id_terceiro_responsavel = table.Column<Guid>(type: "uuid", nullable: true),
                    nome_responsavel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    id_atribuicao = table.Column<Guid>(type: "uuid", nullable: true),
                    protocolo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    pendencia = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    numero_matricula = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    id_documento_certidao = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_solicitacoes_certidao", x => x.id_solicitacao);
                });

            migrationBuilder.CreateTable(
                name: "solicitacoes_certidao_historico",
                schema: "documentos",
                columns: table => new
                {
                    id_historico = table.Column<Guid>(type: "uuid", nullable: false),
                    de = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    para = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    acao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    id_usuario = table.Column<Guid>(type: "uuid", nullable: true),
                    observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    id_solicitacao = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_solicitacoes_certidao_historico", x => x.id_historico);
                    table.ForeignKey(
                        name: "fk_solicitacoes_certidao_historico_solicitacoes_certidao_id_so",
                        column: x => x.id_solicitacao,
                        principalSchema: "documentos",
                        principalTable: "solicitacoes_certidao",
                        principalColumn: "id_solicitacao",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_solicitacoes_certidao_id_beneficiario",
                schema: "documentos",
                table: "solicitacoes_certidao",
                column: "id_beneficiario",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_solicitacoes_certidao_id_tenant",
                schema: "documentos",
                table: "solicitacoes_certidao",
                column: "id_tenant");

            migrationBuilder.CreateIndex(
                name: "ix_solicitacoes_certidao_status_prazo_registro",
                schema: "documentos",
                table: "solicitacoes_certidao",
                columns: new[] { "status", "prazo_registro" });

            migrationBuilder.CreateIndex(
                name: "ix_solicitacoes_certidao_historico_id_solicitacao",
                schema: "documentos",
                table: "solicitacoes_certidao_historico",
                column: "id_solicitacao");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "solicitacoes_certidao_historico",
                schema: "documentos");

            migrationBuilder.DropTable(
                name: "solicitacoes_certidao",
                schema: "documentos");
        }
    }
}
