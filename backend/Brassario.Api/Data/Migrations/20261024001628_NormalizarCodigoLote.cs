using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brassario.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class NormalizarCodigoLote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                  IF EXISTS (SELECT 1 FROM lote_insumo GROUP BY insumo_id, upper(btrim(codigo_lote)) HAVING count(*) > 1) THEN
                    RAISE EXCEPTION 'Existem lotes duplicados (mesmo insumo e código, ignorando maiúsculas e espaços). Corrija antes de aplicar.';
                  END IF;
                  IF EXISTS (SELECT 1 FROM lote_insumo WHERE char_length(btrim(codigo_lote)) > 40 OR btrim(codigo_lote) = '') THEN
                    RAISE EXCEPTION 'Existem lotes com código vazio ou acima de 40 caracteres. Corrija antes de aplicar.';
                  END IF;
                END $$;");
            migrationBuilder.Sql("UPDATE lote_insumo SET codigo_lote = upper(btrim(codigo_lote)) WHERE codigo_lote <> upper(btrim(codigo_lote));");

            migrationBuilder.AlterColumn<string>(
                name: "codigo_lote",
                table: "lote_insumo",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(60)",
                oldMaxLength: 60);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "codigo_lote",
                table: "lote_insumo",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);
        }
    }
}
