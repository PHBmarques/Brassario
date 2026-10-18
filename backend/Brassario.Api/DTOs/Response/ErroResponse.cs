namespace Brassario.Api.DTOs.Response
{
    public record ErroResponse(string Codigo, string Mensagem, IDictionary<string, string[]>? Detalhes = null);
}
