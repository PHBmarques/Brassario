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

        private readonly JwtOptions _opcoes;

        public JwtService(IOptions<JwtOptions> opcoes) => _opcoes = opcoes.Value;

        public (string Token, DateTime ExpiraEm) GerarAccessToken(Usuario usuario)
        {
            var expiraEm = DateTime.UtcNow.AddMinutes(_opcoes.ExpiresMinutes);

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = _opcoes.Issuer,
                Audience = _opcoes.Issuer,
                Expires = expiraEm,
                Subject = new ClaimsIdentity(new[]
                {
                new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, usuario.Email),
                new Claim(ClaimPerfil, usuario.Perfil.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            }),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opcoes.Key)),
                    SecurityAlgorithms.HmacSha256)
            };

            return (new JsonWebTokenHandler().CreateToken(descriptor), expiraEm);
        }
    }
}
