using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Financeiro.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class ViewsRelatorios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE SCHEMA IF NOT EXISTS relatorios;
                CREATE VIEW relatorios.vw_faturas AS
                SELECT id_fatura, id_tenant, id_contrato, competencia, valor, valor_multa_juros, data_vencimento, status, forma_pagamento, data_pagamento, fora_da_inadimplencia
                FROM financeiro.faturas;
                """);
            migrationBuilder.Sql("""
                CREATE SCHEMA IF NOT EXISTS relatorios;
                CREATE VIEW relatorios.vw_contas_pagamento AS
                SELECT id_tenant, cobrancas_habilitadas, cadastro_enviado
                FROM financeiro.contas_pagamento;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS relatorios.vw_faturas;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS relatorios.vw_contas_pagamento;");
        }
    }
}
