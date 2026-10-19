namespace Brassario.Api.Models
{
    public enum StatusEscassez
    {
        /// <summary>Saldo igual ou acima do estoque mínimo.</summary>
        OK,

        /// <summary>Saldo abaixo do estoque mínimo, mas ainda há produto.</summary>
        BAIXO,

        /// <summary>Sem saldo utilizável (nenhum lote não vencido com saldo).</summary>
        VAZIO
    }
}
