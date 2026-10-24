using System;
using Brassario.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brassario.Api.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20261024120000_AddCancelamentoBatelada")]
    public partial class AddCancelamentoBatelada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "cancelado_em",
                table: "batelada",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_cancelamento",
                table: "batelada",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            // Produções já canceladas antes desta migration: o motivo e a data estão no primeiro estorno de cada uma
            migrationBuilder.Sql(@"
                UPDATE batelada b
                SET motivo_cancelamento = e.motivo, cancelado_em = e.data_hora
                FROM (
                    SELECT DISTINCT ON (batelada_id) batelada_id, motivo, data_hora
                    FROM movimentacao_estoque
                    WHERE tipo = 'ESTORNO' AND batelada_id IS NOT NULL
                    ORDER BY batelada_id, data_hora, id
                ) e
                WHERE b.id = e.batelada_id AND b.status = 'CANCELADA' AND b.motivo_cancelamento IS NULL;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_batelada_cancelamento",
                table: "batelada",
                sql: "status = 'CANCELADA' OR (motivo_cancelamento IS NULL AND cancelado_em IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_batelada_cancelamento",
                table: "batelada");

            migrationBuilder.DropColumn(
                name: "cancelado_em",
                table: "batelada");

            migrationBuilder.DropColumn(
                name: "motivo_cancelamento",
                table: "batelada");
        }
    }
}
