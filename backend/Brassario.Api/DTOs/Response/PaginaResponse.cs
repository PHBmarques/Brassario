namespace Brassario.Api.DTOs.Response
{
    /// <summary>Uma página de uma lista. Itens vazios quando a página pedida passa da última.</summary>
    public record PaginaResponse<T>(
        List<T> Itens,
        int Pagina,
        int TamanhoPagina,
        int Total,
        int TotalPaginas)
    {
        public static PaginaResponse<T> Criar(List<T> itens, int pagina, int tamanho, int total) =>
            new(itens, pagina, tamanho, total, (int)Math.Ceiling(total / (double)tamanho));
    }
}
