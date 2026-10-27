using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Catalogo.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class FornecedorDoItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "id_terceiro_fornecedor",
                schema: "catalogo",
                table: "itens",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "id_terceiro_fornecedor",
                schema: "catalogo",
                table: "itens");
        }
    }
}
