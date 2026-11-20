using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Contratos.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class ViewsRelatorios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE SCHEMA IF NOT EXISTS relatorios;
                CREATE VIEW relatorios.vw_contratos AS
                SELECT id_contrato, id_tenant, status, nome_plano, periodicidade, valor_total, data_contratacao, data_fim
                FROM contratos.contratos;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS relatorios.vw_contratos;");
        }
    }
}
