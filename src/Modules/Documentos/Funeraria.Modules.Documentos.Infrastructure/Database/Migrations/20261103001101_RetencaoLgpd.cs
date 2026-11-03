using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Documentos.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class RetencaoLgpd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "anonimizado_em",
                schema: "documentos",
                table: "solicitacoes_certidao",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "anonimizado_em",
                schema: "documentos",
                table: "solicitacoes_certidao");
        }
    }
}
