using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Translado.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class RetencaoLgpd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "anonimizado_em",
                schema: "translado",
                table: "translados",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "anonimizado_em",
                schema: "translado",
                table: "translados");
        }
    }
}
