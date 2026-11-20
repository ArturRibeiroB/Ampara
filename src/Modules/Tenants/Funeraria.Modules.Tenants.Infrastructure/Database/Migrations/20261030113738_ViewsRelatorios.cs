using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Tenants.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class ViewsRelatorios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE SCHEMA IF NOT EXISTS relatorios;
                CREATE VIEW relatorios.vw_funerarias AS
                SELECT id_tenant, nome_fantasia, status
                FROM tenants.funerarias;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS relatorios.vw_funerarias;");
        }
    }
}
