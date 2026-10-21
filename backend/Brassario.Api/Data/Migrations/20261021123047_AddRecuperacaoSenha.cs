using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Brassario.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecuperacaoSenha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "deve_trocar_senha",
                table: "usuario",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "email_recuperacao",
                table: "usuario",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "email_recuperacao_confirmado_em",
                table: "usuario",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "token_usuario",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_id = table.Column<long>(type: "bigint", nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    token_hash = table.Column<string>(type: "char(64)", maxLength: 64, nullable: false),
                    email_destino = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    expira_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_token_usuario", x => x.id);
                    table.CheckConstraint("ck_token_usuario_tipo", "tipo IN ('CONFIRMAR_EMAIL','REDEFINIR_SENHA')");
                    table.ForeignKey(
                        name: "FK_token_usuario_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_token_usuario_usuario_tipo",
                table: "token_usuario",
                columns: new[] { "usuario_id", "tipo" });

            migrationBuilder.CreateIndex(
                name: "uq_token_usuario_hash",
                table: "token_usuario",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "token_usuario");

            migrationBuilder.DropColumn(
                name: "deve_trocar_senha",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "email_recuperacao",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "email_recuperacao_confirmado_em",
                table: "usuario");
        }
    }
}
