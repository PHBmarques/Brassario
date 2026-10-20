using System.Security.Cryptography;
using System.Text;

namespace Brassario.Api.Security
{
    public class RefreshTokenHelper
    {
        /// <summary>Token aleatório de 48 bytes (384 bits) em base64url. Vai só no cookie.</summary>
        public static string Gerar() =>
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        /// <summary>
        /// SHA-256 em hexadecimal (64 caracteres). Como o token já é aleatório e longo, um hash rápido basta
        /// (o BCrypt é para senhas escolhidas por pessoas) e permite achar o token pelo hash no banco.
        /// </summary>
        public static string Hash(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
    }
}
