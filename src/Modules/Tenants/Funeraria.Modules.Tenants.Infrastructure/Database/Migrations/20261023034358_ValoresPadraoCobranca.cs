using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funeraria.Modules.Tenants.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class ValoresPadraoCobranca : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Funerárias que ainda não preencheram multa, juros e desconto recebem os padrões do DEC-12
        /// (2%, 1% ao mês e 10%). Valores já preenchidos pelo gestor não mudam.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE tenants.funerarias
                SET parametros = jsonb_set(
                    parametros,
                    '{cobranca}',
                    COALESCE(parametros->'cobranca', '{}'::jsonb) || jsonb_build_object(
                        'multa_percentual', COALESCE(NULLIF(parametros->'cobranca'->'multa_percentual', 'null'::jsonb), '2'::jsonb),
                        'juros_mensal_percentual', COALESCE(NULLIF(parametros->'cobranca'->'juros_mensal_percentual', 'null'::jsonb), '1'::jsonb),
                        'desconto_anual_percentual', COALESCE(NULLIF(parametros->'cobranca'->'desconto_anual_percentual', 'null'::jsonb), '10'::jsonb)),
                    true);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Não há como saber quais valores eram nulos antes: a reversão mantém os valores.
        }
    }
}
