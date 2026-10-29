using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Funeral.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class ReservasDeLocais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "locais",
                schema: "funeral",
                columns: table => new
                {
                    id_local = table.Column<Guid>(type: "uuid", nullable: false),
                    id_tenant = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    endereco = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    proprio = table.Column<bool>(type: "boolean", nullable: false),
                    intervalo_preparacao_minutos = table.Column<int>(type: "integer", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_locais", x => x.id_local);
                });

            migrationBuilder.CreateTable(
                name: "reservas",
                schema: "funeral",
                columns: table => new
                {
                    id_reserva = table.Column<Guid>(type: "uuid", nullable: false),
                    id_tenant = table.Column<Guid>(type: "uuid", nullable: false),
                    id_local = table.Column<Guid>(type: "uuid", nullable: false),
                    id_funeral = table.Column<Guid>(type: "uuid", nullable: false),
                    inicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fim = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ocupado_ate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    controla_agenda = table.Column<bool>(type: "boolean", nullable: false),
                    cancelada = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reservas", x => x.id_reserva);
                    table.ForeignKey(
                        name: "fk_reservas_locais_id_local",
                        column: x => x.id_local,
                        principalSchema: "funeral",
                        principalTable: "locais",
                        principalColumn: "id_local",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_locais_id_tenant",
                schema: "funeral",
                table: "locais",
                column: "id_tenant");

            migrationBuilder.CreateIndex(
                name: "ix_reservas_id_funeral",
                schema: "funeral",
                table: "reservas",
                column: "id_funeral");

            migrationBuilder.CreateIndex(
                name: "ix_reservas_id_local_inicio",
                schema: "funeral",
                table: "reservas",
                columns: new[] { "id_local", "inicio" });

            migrationBuilder.CreateIndex(
                name: "ix_reservas_id_tenant",
                schema: "funeral",
                table: "reservas",
                column: "id_tenant");

            // Duas reservas ativas do mesmo local próprio não podem se sobrepor, nem chegando ao mesmo tempo (FUN-05).
            // O btree_gist permite combinar a igualdade do local com a sobreposição de períodos no mesmo índice GiST.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");
            migrationBuilder.Sql("""
                ALTER TABLE funeral.reservas ADD CONSTRAINT ex_reservas_sem_conflito
                EXCLUDE USING gist (id_local WITH =, tstzrange(inicio, ocupado_ate) WITH &&)
                WHERE (controla_agenda AND NOT cancelada);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reservas",
                schema: "funeral");

            migrationBuilder.DropTable(
                name: "locais",
                schema: "funeral");
        }
    }
}
