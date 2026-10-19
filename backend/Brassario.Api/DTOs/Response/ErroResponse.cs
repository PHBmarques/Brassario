using System.Text.Json.Serialization;

namespace Brassario.Api.DTOs.Response
{
    public record ErroResponse(int Status,
    string Codigo,
    string Mensagem,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IDictionary<string, string[]>? Detalhes = null);
}
