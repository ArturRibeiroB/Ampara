using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Identidade.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Auditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "auditoria",
                schema: "identidade",
                columns: table => new
                {
                    id_auditoria = table.Column<Guid>(type: "uuid", nullable: false),
                    id_tenant = table.Column<Guid>(type: "uuid", nullable: true),
                    plataforma = table.Column<bool>(type: "boolean", nullable: false),
                    id_usuario = table.Column<Guid>(type: "uuid", nullable: true),
                    acao = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    entidade = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    id_entidade = table.Column<Guid>(type: "uuid", nullable: true),
                    valores_anteriores = table.Column<string>(type: "jsonb", nullable: true),
                    valores_novos = table.Column<string>(type: "jsonb", nullable: true),
                    data_hora = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auditoria", x => x.id_auditoria);
                });

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_entidade_id_entidade",
                schema: "identidade",
                table: "auditoria",
                columns: new[] { "entidade", "id_entidade" });

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_id_tenant_data_hora",
                schema: "identidade",
                table: "auditoria",
                columns: new[] { "id_tenant", "data_hora" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auditoria",
                schema: "identidade");
        }
    }
}
