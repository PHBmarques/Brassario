using System.ComponentModel.DataAnnotations;

namespace Brassario.Api.DTOs.Request
{
    public class UsuarioRequest
    {
        [Required(ErrorMessage = "O nome é obrigatório.")]
        [StringLength(120, ErrorMessage = "O nome deve ter no máximo 120 caracteres.")]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "O e-mail é obrigatório.")]
        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        [StringLength(160, ErrorMessage = "O e-mail deve ter no máximo 160 caracteres.")]
        public string Email { get; set; } = string.Empty;

        /// <summary>ADMIN ou OPERADOR.</summary>
        [Required(ErrorMessage = "O perfil é obrigatório.")]
        public string Perfil { get; set; } = string.Empty;

        /// <summary>Mínimo de 8 caracteres. Vira hash BCrypt e nunca é devolvida pela API.</summary>
        [Required(ErrorMessage = "A senha é obrigatória.")]
        public string Senha { get; set; } = string.Empty;

        // Se o objeto for logado por engano, a senha não aparece
        public override string ToString() => $"UsuarioRequest {{ Nome = {Nome}, Email = {Email}, Perfil = {Perfil}, Senha = *** }}";
    }

    /// <summary>Dados para editar um usuário (somente ADMIN). A senha é opcional: só muda se for informada.</summary>
    public class UsuarioAtualizarRequest
    {
        [Required(ErrorMessage = "O nome é obrigatório.")]
        [StringLength(120, ErrorMessage = "O nome deve ter no máximo 120 caracteres.")]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "O e-mail é obrigatório.")]
        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        [StringLength(160, ErrorMessage = "O e-mail deve ter no máximo 160 caracteres.")]
        public string Email { get; set; } = string.Empty;

        /// <summary>ADMIN ou OPERADOR.</summary>
        [Required(ErrorMessage = "O perfil é obrigatório.")]
        public string Perfil { get; set; } = string.Empty;

        /// <summary>Opcional. Quando informada, troca a senha (mínimo de 8 caracteres) e encerra as sessões abertas do usuário.</summary>
        public string? Senha { get; set; }

        public override string ToString() => $"UsuarioAtualizarRequest {{ Nome = {Nome}, Email = {Email}, Perfil = {Perfil}, Senha = *** }}";
    }
}
