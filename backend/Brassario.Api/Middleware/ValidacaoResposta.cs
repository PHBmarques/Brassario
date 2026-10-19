using System.Text.Json;
using Brassario.Api.DTOs.Response;
using Microsoft.AspNetCore.Mvc;

namespace Brassario.Api.Middleware
{
    public class ValidacaoResposta
    {
        public static IActionResult Criar(ActionContext contexto)
        {
            var detalhes = new Dictionary<string, List<string>>();

            foreach (var (chave, entrada) in contexto.ModelState)
            {
                if (entrada.Errors.Count == 0) continue;

                // Chave vazia ou "$" = erro no corpo inteiro (por exemplo, JSON malformado)
                var campo = string.IsNullOrEmpty(chave) || chave.StartsWith('$')
                    ? "corpo"
                    : JsonNamingPolicy.CamelCase.ConvertName(chave);

                if (!detalhes.TryGetValue(campo, out var lista))
                    detalhes[campo] = lista = new List<string>();

                // Mensagem técnica de JSON não é para o usuário
                lista.AddRange(entrada.Errors.Select(e =>
                    campo == "corpo" ? "O corpo da requisição é inválido." : e.ErrorMessage));
            }

            var erro = new ErroResponse(
                400, "VALIDACAO", "Dados inválidos. Verifique os campos informados.",
                detalhes.ToDictionary(d => d.Key, d => d.Value.ToArray()));

            return new BadRequestObjectResult(erro);
        }
    }
}
