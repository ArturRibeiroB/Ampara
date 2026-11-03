using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Clientes.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class RetencaoLgpd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "anonimizado_em",
                schema: "clientes",
                table: "dependentes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "anonimizado_em",
                schema: "clientes",
                table: "clientes",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "anonimizado_em",
                schema: "clientes",
                table: "dependentes");

            migrationBuilder.DropColumn(
                name: "anonimizado_em",
                schema: "clientes",
                table: "clientes");
        }
    }
}
