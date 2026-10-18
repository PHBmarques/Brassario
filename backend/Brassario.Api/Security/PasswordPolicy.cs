using System.Text;

namespace Brassario.Api.Security
{
    /// <summary>
    /// Regras de senha do sistema: no mínimo 8 caracteres.
    /// O limite de 72 bytes existe porque o BCrypt ignora o que passa disso.
    /// </summary>
    public static class PasswordPolicy
    {
        public const int MinimoCaracteres = 8;
        public const int MaximoBytes = 72;

        public static IReadOnlyList<string> Validar(string? senha)
        {
            var erros = new List<string>();

            if (string.IsNullOrWhiteSpace(senha))
            {
                erros.Add("A senha é obrigatória.");
                return erros;
            }

            if (senha.Length < MinimoCaracteres)
                erros.Add($"A senha deve ter no mínimo {MinimoCaracteres} caracteres.");

            if (Encoding.UTF8.GetByteCount(senha) > MaximoBytes)
                erros.Add($"A senha deve ter no máximo {MaximoBytes} bytes (cerca de {MaximoBytes} caracteres).");

            return erros;
        }

        /// <summary>Lança ArgumentException (sem incluir a senha na mensagem) se a senha não cumprir as regras.</summary>
        public static void Garantir(string? senha)
        {
            var erros = Validar(senha);
            if (erros.Count > 0)
                throw new ArgumentException(string.Join(" ", erros), nameof(senha));
        }
    }
}
