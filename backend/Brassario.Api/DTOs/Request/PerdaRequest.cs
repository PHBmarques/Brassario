namespace Brassario.Api.DTOs.Request
{
    public class PerdaRequest
    {
        /// <summary>Quantidade perdida, na unidade do insumo (maior que zero, até 3 casas decimais e até o saldo do lote).</summary>
        public decimal? Quantidade { get; set; }

        /// <summary>Motivo da perda (de 5 a 255 caracteres). A lista de motivos da tela é só do front.</summary>
        public string? Motivo { get; set; }
    }
}
