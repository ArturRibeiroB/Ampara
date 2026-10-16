using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Funeraria.Modules.Identidade.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identidade");

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "identidade",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    content = table.Column<string>(type: "jsonb", nullable: false),
                    id_tenant = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "identidade",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                schema: "identidade",
                columns: table => new
                {
                    id_usuario = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuarios", x => x.id_usuario);
                });

            migrationBuilder.CreateTable(
                name: "role_claims",
                schema: "identidade",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_role_claims_asp_net_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "identidade",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                schema: "identidade",
                columns: table => new
                {
                    id_refresh_token = table.Column<Guid>(type: "uuid", nullable: false),
                    id_usuario = table.Column<Guid>(type: "uuid", nullable: false),
                    id_tenant = table.Column<Guid>(type: "uuid", nullable: true),
                    token_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expira_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revogado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    substituido_por = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id_refresh_token);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_usuarios_id_usuario",
                        column: x => x.id_usuario,
                        principalSchema: "identidade",
                        principalTable: "usuarios",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuario_claims",
                schema: "identidade",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_usuario_claims_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identidade",
                        principalTable: "usuarios",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuario_logins",
                schema: "identidade",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    provider_key = table.Column<string>(type: "text", nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "fk_usuario_logins_usuarios_user_id",
                        column: x => x.user_id,
                        principalSchema: "identidade",
                        principalTable: "usuarios",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuario_roles",
                schema: "identidade",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_usuario_roles_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "identidade",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_usuario_roles_usuarios_user_id",
                        column: x => x.user_id,
                        principalSchema: "identidade",
                        principalTable: "usuarios",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuario_tokens",
                schema: "identidade",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "fk_usuario_tokens_usuarios_user_id",
                        column: x => x.user_id,
                        principalSchema: "identidade",
                        principalTable: "usuarios",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuario_vinculos",
                schema: "identidade",
                columns: table => new
                {
                    id_vinculo = table.Column<Guid>(type: "uuid", nullable: false),
                    id_usuario = table.Column<Guid>(type: "uuid", nullable: false),
                    id_tenant = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    id_cliente = table.Column<Guid>(type: "uuid", nullable: true),
                    id_terceiro = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo_terceiro = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario_vinculos", x => x.id_vinculo);
                    table.ForeignKey(
                        name: "fk_usuario_vinculos_usuarios_id_usuario",
                        column: x => x.id_usuario,
                        principalSchema: "identidade",
                        principalTable: "usuarios",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_occurred_on_utc",
                schema: "identidade",
                table: "outbox_messages",
                column: "occurred_on_utc",
                filter: "processed_on_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_id_usuario",
                schema: "identidade",
                table: "refresh_tokens",
                column: "id_usuario",
                filter: "revogado_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                schema: "identidade",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_role_claims_role_id",
                schema: "identidade",
                table: "role_claims",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "identidade",
                table: "roles",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuario_claims_user_id",
                schema: "identidade",
                table: "usuario_claims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_logins_user_id",
                schema: "identidade",
                table: "usuario_logins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_roles_role_id",
                schema: "identidade",
                table: "usuario_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_vinculos_id_tenant",
                schema: "identidade",
                table: "usuario_vinculos",
                column: "id_tenant");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_vinculos_id_usuario_id_tenant",
                schema: "identidade",
                table: "usuario_vinculos",
                columns: new[] { "id_usuario", "id_tenant" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "identidade",
                table: "usuarios",
                column: "normalized_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "identidade",
                table: "usuarios",
                column: "normalized_user_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "identidade");

            migrationBuilder.DropTable(
                name: "refresh_tokens",
                schema: "identidade");

            migrationBuilder.DropTable(
                name: "role_claims",
                schema: "identidade");

            migrationBuilder.DropTable(
                name: "usuario_claims",
                schema: "identidade");

            migrationBuilder.DropTable(
                name: "usuario_logins",
                schema: "identidade");

            migrationBuilder.DropTable(
                name: "usuario_roles",
                schema: "identidade");

            migrationBuilder.DropTable(
                name: "usuario_tokens",
                schema: "identidade");

            migrationBuilder.DropTable(
                name: "usuario_vinculos",
                schema: "identidade");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "identidade");

            migrationBuilder.DropTable(
                name: "usuarios",
                schema: "identidade");
        }
    }
}
