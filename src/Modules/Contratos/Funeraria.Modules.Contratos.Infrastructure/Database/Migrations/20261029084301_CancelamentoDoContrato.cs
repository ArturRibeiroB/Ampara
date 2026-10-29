using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Contratos.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class CancelamentoDoContrato : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "motivo_cancelamento",
                schema: "contratos",
                table: "contratos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "motivo_cancelamento",
                schema: "contratos",
                table: "contratos");
        }
    }
}
