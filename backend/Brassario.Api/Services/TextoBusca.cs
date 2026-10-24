using System.Globalization;
using System.Text;

namespace Brassario.Api.Services
{
    /// <summary>Normaliza texto para busca: sem acento, sem diferenciar maiúsculas e sem espaços sobrando.</summary>
    public static class TextoBusca
    {
        public static string Normalizar(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

            var decomposto = texto.Trim().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposto.Length);
            var ultimoFoiEspaco = false;

            foreach (var c in decomposto)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;   // tira o acento

                if (char.IsWhiteSpace(c))
                {
                    if (!ultimoFoiEspaco) sb.Append(' ');
                    ultimoFoiEspaco = true;
                }
                else
                {
                    sb.Append(char.ToLowerInvariant(c));
                    ultimoFoiEspaco = false;
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        /// <summary>
        /// Filtra e ordena pelo nome: quem começa com o termo vem primeiro, depois quem apenas contém, em ordem alfabética.
        /// </summary>
        public static IEnumerable<T> Filtrar<T>(IEnumerable<T> itens, string? busca, Func<T, string> nome)
        {
            var termo = Normalizar(busca);

            return itens
                .Select(i => (Item: i, Nome: Normalizar(nome(i))))
                .Where(x => x.Nome.Contains(termo, StringComparison.Ordinal))
                .OrderBy(x => x.Nome.StartsWith(termo, StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(x => x.Nome, StringComparer.Ordinal)
                .Select(x => x.Item);
        }
    }
}
