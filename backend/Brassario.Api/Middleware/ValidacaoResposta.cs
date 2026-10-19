using System.Text.Json;
using Brassario.Api.DTOs.Response;
using Microsoft.AspNetCore.Mvc;

namespace Brassario.Api.Middleware
{
    public class ValidacaoResposta
    {
        private const string MensagemFormato =
        "Valor inválido para este campo. Confira o formato (datas: AAAA-MM-DD; números: use ponto nos decimais).";

        public static IActionResult Criar(ActionContext contexto)
        {
            var parametros = contexto.ActionDescriptor.Parameters
                .Select(p => p.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var detalhes = new Dictionary<string, List<string>>();
            var temErroDeCorpo = false;

            foreach (var (chave, entrada) in contexto.ModelState)
            {
                if (entrada.Errors.Count == 0) continue;

                // Chave vazia, "$" ou o nome do parâmetro (ex.: "request"): problema no corpo inteiro
                if (string.IsNullOrEmpty(chave) || chave == "$" || parametros.Contains(chave))
                {
                    temErroDeCorpo = true;
                    continue;
                }

                // "$.dataValidade": o JSON tem o campo, mas o valor não tem o formato esperado
                var formatoInvalido = chave.StartsWith("$.");
                var campo = Camel(formatoInvalido ? chave[2..] : chave);

                if (!detalhes.TryGetValue(campo, out var lista))
                    detalhes[campo] = lista = new List<string>();

                if (formatoInvalido) lista.Add(MensagemFormato);
                else lista.AddRange(entrada.Errors.Select(e => e.ErrorMessage));
            }

            // O aviso genérico do corpo só aparece quando não há nenhum campo específico para apontar
            if (temErroDeCorpo && detalhes.Count == 0)
                detalhes["corpo"] = new List<string> { "O corpo da requisição é obrigatório e deve ser um JSON válido." };

            var erro = new ErroResponse(
                400, "VALIDACAO", "Dados inválidos. Verifique os campos informados.",
                detalhes.ToDictionary(d => d.Key, d => d.Value.Distinct().ToArray()));

            return new BadRequestObjectResult(erro);
        }

        // "Itens[0].Quantidade" -> "itens[0].quantidade"
        private static string Camel(string caminho) =>
            string.Join(".", caminho.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
    }
}
