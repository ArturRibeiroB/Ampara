using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Contratos.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class ConsultorDoContrato : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "id_consultor",
                schema: "contratos",
                table: "contratos",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "id_consultor",
                schema: "contratos",
                table: "contratos");
        }
    }
}
