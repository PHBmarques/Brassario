namespace Brassario.Api.DTOs.Request
{
    public class BateladaRequest
    {
        /// <summary>Id da receita (precisa estar ativa).</summary>
        public long ReceitaId { get; set; }

        /// <summary>Volume a produzir, em litros (maior que zero, até 2 casas decimais).</summary>
        public decimal? VolumePlanejado { get; set; }

        /// <summary>Observação opcional (até 255 caracteres).</summary>
        public string? Observacoes { get; set; }
    }
}
