using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Brassario.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fornecedor",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    contato = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fornecedor", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "insumo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    categoria = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    unidade_medida = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    estoque_minimo = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    dias_validade_critica = table.Column<int>(type: "integer", nullable: false),
                    dias_validade_atencao = table.Column<int>(type: "integer", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_insumo", x => x.id);
                    table.CheckConstraint("ck_insumo_categoria", "categoria IN ('MALTE','LUPULO','LEVEDURA','ADJUNTO','OUTRO')");
                    table.CheckConstraint("ck_insumo_dias_atencao", "dias_validade_atencao >= dias_validade_critica");
                    table.CheckConstraint("ck_insumo_dias_critica", "dias_validade_critica >= 0");
                    table.CheckConstraint("ck_insumo_estoque_minimo", "estoque_minimo >= 0");
                    table.CheckConstraint("ck_insumo_unidade", "unidade_medida IN ('KG','G','L','ML','UN')");
                });

            migrationBuilder.CreateTable(
                name: "receita",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    estilo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    volume_base_litros = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    modo_preparo = table.Column<string>(type: "text", nullable: true),
                    ativa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receita", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuario",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    email = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    senha_hash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    perfil = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario", x => x.id);
                    table.CheckConstraint("ck_usuario_perfil", "perfil IN ('ADMIN','OPERADOR')");
                });

            migrationBuilder.CreateTable(
                name: "lote_insumo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    insumo_id = table.Column<long>(type: "bigint", nullable: false),
                    fornecedor_id = table.Column<long>(type: "bigint", nullable: true),
                    codigo_lote = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    data_entrada = table.Column<DateOnly>(type: "date", nullable: false),
                    data_validade = table.Column<DateOnly>(type: "date", nullable: false),
                    quantidade_inicial = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    quantidade_atual = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    preco_unitario = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    versao = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lote_insumo", x => x.id);
                    table.CheckConstraint("ck_lote_preco", "preco_unitario >= 0");
                    table.CheckConstraint("ck_lote_quantidade_atual_max", "quantidade_atual <= quantidade_inicial");
                    table.CheckConstraint("ck_lote_quantidade_atual_min", "quantidade_atual >= 0");
                    table.CheckConstraint("ck_lote_quantidade_inicial", "quantidade_inicial > 0");
                    table.CheckConstraint("ck_lote_validade", "data_validade >= data_entrada");
                    table.ForeignKey(
                        name: "FK_lote_insumo_fornecedor_fornecedor_id",
                        column: x => x.fornecedor_id,
                        principalTable: "fornecedor",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lote_insumo_insumo_insumo_id",
                        column: x => x.insumo_id,
                        principalTable: "insumo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receita_item",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    receita_id = table.Column<long>(type: "bigint", nullable: false),
                    insumo_id = table.Column<long>(type: "bigint", nullable: false),
                    quantidade = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receita_item", x => x.id);
                    table.ForeignKey(
                        name: "FK_receita_item_insumo_insumo_id",
                        column: x => x.insumo_id,
                        principalTable: "insumo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_receita_item_receita_receita_id",
                        column: x => x.receita_id,
                        principalTable: "receita",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "batelada",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    receita_id = table.Column<long>(type: "bigint", nullable: false),
                    usuario_id = table.Column<long>(type: "bigint", nullable: false),
                    volume_planejado = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    volume_final = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    custo_total = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    data_producao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    observacoes = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_batelada", x => x.id);
                    table.CheckConstraint("ck_batelada_custo", "custo_total >= 0");
                    table.CheckConstraint("ck_batelada_status", "status IN ('EM_ANDAMENTO','FINALIZADA','CANCELADA')");
                    table.CheckConstraint("ck_batelada_volume_final", "volume_final IS NULL OR volume_final > 0");
                    table.CheckConstraint("ck_batelada_volume_planejado", "volume_planejado > 0");
                    table.ForeignKey(
                        name: "FK_batelada_receita_receita_id",
                        column: x => x.receita_id,
                        principalTable: "receita",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_batelada_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_token",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_id = table.Column<long>(type: "bigint", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    expira_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revogado = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_token", x => x.id);
                    table.ForeignKey(
                        name: "FK_refresh_token_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "batelada_consumo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    batelada_id = table.Column<long>(type: "bigint", nullable: false),
                    lote_insumo_id = table.Column<long>(type: "bigint", nullable: false),
                    quantidade = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    custo = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_batelada_consumo", x => x.id);
                    table.CheckConstraint("ck_batelada_consumo_custo", "custo >= 0");
                    table.CheckConstraint("ck_batelada_consumo_quantidade", "quantidade > 0");
                    table.ForeignKey(
                        name: "FK_batelada_consumo_batelada_batelada_id",
                        column: x => x.batelada_id,
                        principalTable: "batelada",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_batelada_consumo_lote_insumo_lote_insumo_id",
                        column: x => x.lote_insumo_id,
                        principalTable: "lote_insumo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "movimentacao_estoque",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lote_insumo_id = table.Column<long>(type: "bigint", nullable: false),
                    usuario_id = table.Column<long>(type: "bigint", nullable: false),
                    batelada_id = table.Column<long>(type: "bigint", nullable: true),
                    tipo = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    quantidade = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    motivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    data_hora = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movimentacao_estoque", x => x.id);
                    table.CheckConstraint("ck_movimentacao_motivo", "tipo NOT IN ('AJUSTE','PERDA') OR (motivo IS NOT NULL AND char_length(btrim(motivo)) >= 5)");
                    table.CheckConstraint("ck_movimentacao_tipo", "tipo IN ('ENTRADA','SAIDA_PRODUCAO','AJUSTE','PERDA','ESTORNO')");
                    table.ForeignKey(
                        name: "FK_movimentacao_estoque_batelada_batelada_id",
                        column: x => x.batelada_id,
                        principalTable: "batelada",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movimentacao_estoque_lote_insumo_lote_insumo_id",
                        column: x => x.lote_insumo_id,
                        principalTable: "lote_insumo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movimentacao_estoque_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_batelada_data_producao",
                table: "batelada",
                column: "data_producao");

            migrationBuilder.CreateIndex(
                name: "IX_batelada_receita_id",
                table: "batelada",
                column: "receita_id");

            migrationBuilder.CreateIndex(
                name: "IX_batelada_usuario_id",
                table: "batelada",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "uq_batelada_codigo",
                table: "batelada",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_batelada_consumo_batelada_id",
                table: "batelada_consumo",
                column: "batelada_id");

            migrationBuilder.CreateIndex(
                name: "IX_batelada_consumo_lote_insumo_id",
                table: "batelada_consumo",
                column: "lote_insumo_id");

            migrationBuilder.CreateIndex(
                name: "uq_fornecedor_nome",
                table: "fornecedor",
                column: "nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_insumo_nome",
                table: "insumo",
                column: "nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lote_insumo_fornecedor_id",
                table: "lote_insumo",
                column: "fornecedor_id");

            migrationBuilder.CreateIndex(
                name: "ix_lote_insumo_insumo_validade",
                table: "lote_insumo",
                columns: new[] { "insumo_id", "data_validade" });

            migrationBuilder.CreateIndex(
                name: "ix_lote_insumo_validade",
                table: "lote_insumo",
                column: "data_validade");

            migrationBuilder.CreateIndex(
                name: "uq_lote_insumo_codigo",
                table: "lote_insumo",
                columns: new[] { "insumo_id", "codigo_lote" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_movimentacao_estoque_batelada_id",
                table: "movimentacao_estoque",
                column: "batelada_id");

            migrationBuilder.CreateIndex(
                name: "IX_movimentacao_estoque_usuario_id",
                table: "movimentacao_estoque",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimentacao_lote_data",
                table: "movimentacao_estoque",
                columns: new[] { "lote_insumo_id", "data_hora" });

            migrationBuilder.CreateIndex(
                name: "IX_receita_item_insumo_id",
                table: "receita_item",
                column: "insumo_id");

            migrationBuilder.CreateIndex(
                name: "IX_receita_item_receita_id",
                table: "receita_item",
                column: "receita_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_token_usuario_id",
                table: "refresh_token",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "uq_refresh_token_hash",
                table: "refresh_token",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_usuario_email",
                table: "usuario",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "batelada_consumo");

            migrationBuilder.DropTable(
                name: "movimentacao_estoque");

            migrationBuilder.DropTable(
                name: "receita_item");

            migrationBuilder.DropTable(
                name: "refresh_token");

            migrationBuilder.DropTable(
                name: "batelada");

            migrationBuilder.DropTable(
                name: "lote_insumo");

            migrationBuilder.DropTable(
                name: "receita");

            migrationBuilder.DropTable(
                name: "usuario");

            migrationBuilder.DropTable(
                name: "fornecedor");

            migrationBuilder.DropTable(
                name: "insumo");
        }
    }
}
