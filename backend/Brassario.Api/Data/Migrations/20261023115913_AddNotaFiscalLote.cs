using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brassario.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotaFiscalLote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "nota_fiscal",
                table: "lote_insumo",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "nota_fiscal",
                table: "lote_insumo");
        }
    }
}
