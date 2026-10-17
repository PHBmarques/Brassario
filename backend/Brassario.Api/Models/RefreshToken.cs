using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Brassario.Api.Models
{
    [Table("refresh_token")]
    public class RefreshToken
    {
        public long Id { get; set; }

        public long UsuarioId { get; set; }
        public Usuario Usuario { get; set; } = null!;

        // Guarda só o hash do token, nunca o token em si
        [Required, MaxLength(100)]
        public string TokenHash { get; set; } = string.Empty;

        public DateTime ExpiraEm { get; set; }

        public bool Revogado { get; set; }
    }
}
