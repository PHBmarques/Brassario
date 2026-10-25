using System.ComponentModel.DataAnnotations;

namespace Brassario.Api.DTOs.Request
{
    public class LoginRequest
    {
        [Required(ErrorMessage = "O e-mail é obrigatório.")]
        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "A senha é obrigatória.")]
        public string Senha { get; set; } = string.Empty;

        /// <summary>Caixa "manter conectado" da tela de login. Aceita para não ser tratada como campo desconhecido; a sessão dura sempre 30 dias.</summary>
        public bool? ManterConectado { get; set; }

        // Se alguém logar o objeto por engano, a senha não aparece no log
        public override string ToString() => $"LoginRequest {{ Email = {Email}, Senha = *** }}";
    }
}
