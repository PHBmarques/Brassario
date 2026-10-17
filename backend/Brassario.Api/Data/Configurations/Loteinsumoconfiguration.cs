using Brassario.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brassario.Api.Data.Configurations
{
    public class Loteinsumoconfiguration: IEntityTypeConfiguration<LoteInsumo>
    {
        public void Configure(EntityTypeBuilder<LoteInsumo> builder)
        {
            builder.ToTable("lote_insumo", t =>
            {
                // O saldo nunca fica negativo nem passa da quantidade recebida
                t.HasCheckConstraint("ck_lote_quantidade_atual_min", "quantidade_atual >= 0");
                t.HasCheckConstraint("ck_lote_quantidade_atual_max", "quantidade_atual <= quantidade_inicial");
                t.HasCheckConstraint("ck_lote_quantidade_inicial", "quantidade_inicial > 0");
                t.HasCheckConstraint("ck_lote_validade", "data_validade >= data_entrada");
                t.HasCheckConstraint("ck_lote_preco", "preco_unitario >= 0");
            });

            builder.HasKey(x => x.Id);

            // UNIQUE: (insumo, código do lote)
            builder.HasIndex(x => new { x.InsumoId, x.CodigoLote })
                .IsUnique()
                .HasDatabaseName("uq_lote_insumo_codigo");

            // Índice da consulta FEFO: lotes do insumo em ordem de validade
            builder.HasIndex(x => new { x.InsumoId, x.DataValidade })
                .HasDatabaseName("ix_lote_insumo_insumo_validade");

            // Índice dos alertas de validade
            builder.HasIndex(x => x.DataValidade).HasDatabaseName("ix_lote_insumo_validade");

            builder.HasOne(x => x.Insumo)
                .WithMany(i => i.Lotes)
                .HasForeignKey(x => x.InsumoId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Fornecedor)
                .WithMany(f => f.Lotes)
                .HasForeignKey(x => x.FornecedorId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
