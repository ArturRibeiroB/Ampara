using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Financeiro.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class FaturaForaDaInadimplencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "fora_da_inadimplencia",
                schema: "financeiro",
                table: "faturas",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fora_da_inadimplencia",
                schema: "financeiro",
                table: "faturas");
        }
    }
}
