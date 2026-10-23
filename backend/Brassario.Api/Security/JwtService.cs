using System.Security.Claims;
using System.Text;
using Brassario.Api.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Brassario.Api.Security
{
    public class JwtService : IJwtService
    {
        public const string ClaimPerfil = "role";
        public const string ClaimTrocaSenha = "pwd_change";

        private readonly JwtOptions _opcoes;

        public JwtService(IOptions<JwtOptions> opcoes) => _opcoes = opcoes.Value;

        public (string Token, DateTime ExpiraEm) GerarAccessToken(Usuario usuario)
        {
            var expiraEm = DateTime.UtcNow.AddMinutes(_opcoes.ExpiresMinutes);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
                new(JwtRegisteredClaimNames.Email, usuario.Email),
                new(ClaimPerfil, usuario.Perfil.ToString()),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            // Troca de senha pendente: o token só serve para concluir o primeiro acesso
            if (usuario.DeveTrocarSenha)
                claims.Add(new Claim(ClaimTrocaSenha, "1"));

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = _opcoes.Issuer,
                Audience = _opcoes.Issuer,
                Expires = expiraEm,
                Subject = new ClaimsIdentity(claims),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opcoes.Key)),
                    SecurityAlgorithms.HmacSha256)
            };

            return (new JsonWebTokenHandler().CreateToken(descriptor), expiraEm);
        }
    }
}