using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Tenants.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class LogoFuneraria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "logo_chave",
                schema: "tenants",
                table: "funerarias",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "logo_chave",
                schema: "tenants",
                table: "funerarias");
        }
    }
}
