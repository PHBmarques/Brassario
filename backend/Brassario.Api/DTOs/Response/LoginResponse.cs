namespace Brassario.Api.DTOs.Response
{
    public record LoginResponse(string AccessToken, string TokenType, int ExpiresIn, UsuarioResponse Usuario)
    {
        // Também no nível de cima, para o front decidir a tela logo após o login
        public bool DeveTrocarSenha => Usuario.DeveTrocarSenha;
        public bool EmailRecuperacaoConfirmado => Usuario.EmailRecuperacaoConfirmado;
    }
}