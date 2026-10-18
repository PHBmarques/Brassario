namespace Brassario.Api.Security
{
    public class PasswordHasher : IPasswordHasher
    {
 
        private const int FatorDeCusto = 11;

        public string Hash(string senha)
        {
            PasswordPolicy.Garantir(senha);
            return BCrypt.Net.BCrypt.HashPassword(senha, FatorDeCusto);
        }

        public bool Verify(string senha, string hash)
        {
            if (string.IsNullOrEmpty(senha) || string.IsNullOrEmpty(hash))
                return false;

            try
            {
                return BCrypt.Net.BCrypt.Verify(senha, hash);
            }
            catch (Exception)
            {
                // Hash em formato inválido: trata como senha incorreta, sem expor detalhes
                return false;
            }
        }
    }
}
