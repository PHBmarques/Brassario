namespace Brassario.Api.DTOs.Response
{
    public record LoginResponse(string AccessToken, string TokenType, int ExpiresIn, UsuarioResponse Usuario);

}
