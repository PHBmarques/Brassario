using System.Security.Cryptography;

namespace Brassario.Api.Security
{
    /// <summary>
    /// Gera a senha temporária que o administrador entrega ao usuário: 12 caracteres aleatórios
    /// (RandomNumberGenerator), com maiúscula, minúscula e número. Sem caracteres ambíguos (0, O, 1, l, I).
    /// </summary>
    public static class GeradorSenhaTemporaria
    {
        public const int Tamanho = 12;

        private const string Maiusculas = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        private const string Minusculas = "abcdefghijkmnpqrstuvwxyz";
        private const string Digitos = "23456789";

        public static string Gerar()
        {
            var todos = Maiusculas + Minusculas + Digitos;
            var caracteres = new char[Tamanho];

            caracteres[0] = Sortear(Maiusculas);
            caracteres[1] = Sortear(Minusculas);
            caracteres[2] = Sortear(Digitos);
            for (var i = 3; i < Tamanho; i++)
                caracteres[i] = Sortear(todos);

            // Embaralha (Fisher-Yates) para as três garantidas não ficarem sempre no começo
            for (var i = Tamanho - 1; i > 0; i--)
            {
                var j = RandomNumberGenerator.GetInt32(i + 1);
                (caracteres[i], caracteres[j]) = (caracteres[j], caracteres[i]);
            }

            return new string(caracteres);
        }

        private static char Sortear(string alfabeto) => alfabeto[RandomNumberGenerator.GetInt32(alfabeto.Length)];
    }
}