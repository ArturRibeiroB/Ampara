using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Translado.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class ViewsRelatorios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE SCHEMA IF NOT EXISTS relatorios;
                CREATE VIEW relatorios.vw_translados AS
                SELECT id_translado, id_tenant, id_funeral, status, tipo, data_prevista
                FROM translado.translados;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS relatorios.vw_translados;");
        }
    }
}
