using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Brassario.Api.Models
{
    [Table("usuario")]
    public class Usuario
    {
        public long Id { get; set; }



        [Required, MaxLength(120)]
        public string Nome { get; set; } = string.Empty;

        [Required, MaxLength(160)]
        public string Email { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string SenhaHash { get; set; } = string.Empty;

        public PerfilUsuario Perfil { get; set; } = PerfilUsuario.OPERADOR;

        public bool Ativo { get; set; } = true;

        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

        [MaxLength(160)]
        public string? EmailRecuperacao { get; set; }

        public DateTime? EmailRecuperacaoConfirmadoEm { get; set; }

        public bool DeveTrocarSenha { get; set; }

        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        public ICollection<Batelada> Bateladas { get; set; } = new List<Batelada>();
        public ICollection<MovimentacaoEstoque> Movimentacoes { get; set; } = new List<MovimentacaoEstoque>();
        public ICollection<TokenUsuario> TokensUsuario { get; set; } = new List<TokenUsuario>();
    }
}
