using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Tenants.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class ConfiguracoesFuneraria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "identidade_visual",
                schema: "tenants",
                table: "funerarias",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "parametros",
                schema: "tenants",
                table: "funerarias",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            // Funerárias já cadastradas aparecem com o próprio nome e contato até o gestor configurar.
            migrationBuilder.Sql(
                """
                UPDATE tenants.funerarias
                SET identidade_visual = jsonb_strip_nulls(jsonb_build_object(
                    'nome_exibicao', nome_fantasia,
                    'email', email_contato,
                    'telefone', telefone))
                WHERE identidade_visual = '{}'::jsonb;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "identidade_visual",
                schema: "tenants",
                table: "funerarias");

            migrationBuilder.DropColumn(
                name: "parametros",
                schema: "tenants",
                table: "funerarias");
        }
    }
}
