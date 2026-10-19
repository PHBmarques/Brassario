using Brassario.Api.Exceptions;
using Brassario.Api.Models;

namespace Brassario.Api.Services
{
    public class FiltroCategoria
    {
        public static CategoriaInsumo? Interpretar(string? categoria)
        {
            if (string.IsNullOrWhiteSpace(categoria)) return null;

            if (!Enum.TryParse<CategoriaInsumo>(categoria.Trim(), true, out var valor) || !Enum.IsDefined(valor))
                throw new ValidacaoException("categoria",
                    $"Categoria inválida. Use: {string.Join(", ", Enum.GetNames<CategoriaInsumo>())}.");

            return valor;
        }
    }
}
