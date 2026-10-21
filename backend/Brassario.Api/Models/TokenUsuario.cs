using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Brassario.Api.Models
{
    [Table("token_usuario")]
    public class TokenUsuario
    {
        public long Id { get; set; }

        public long UsuarioId { get; set; }
        public Usuario Usuario { get; set; } = null!;

        public TipoTokenUsuario Tipo { get; set; }

        // SHA-256 do token em hexadecimal (64 caracteres). Nunca o token em si.
        [Required, StringLength(64, MinimumLength = 64)]
        public string TokenHash { get; set; } = string.Empty;

        // Só para CONFIRMAR_EMAIL: o e-mail que está sendo confirmado
        [MaxLength(160)]
        public string? EmailDestino { get; set; }

        public DateTime ExpiraEm { get; set; }

        public DateTime? UsadoEm { get; set; }

        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    }
}