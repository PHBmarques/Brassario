using Brassario.Api.Security;
using Xunit;

namespace Brassario.Tests
{
    public class PasswordHasherTests
    {
        private readonly PasswordHasher _hasher = new();

        [Fact]
        public void Hash_NaoGuardaASenhaEmTextoPuro()
        {
            var hash = _hasher.Hash("Senha@1234");

            Assert.NotEqual("Senha@1234", hash);
            Assert.DoesNotContain("Senha@1234", hash);
            Assert.StartsWith("$2", hash); // formato BCrypt
        }

        [Fact]
        public void Verify_RetornaTrueParaSenhaCorreta()
        {
            var hash = _hasher.Hash("Senha@1234");
            Assert.True(_hasher.Verify("Senha@1234", hash));
        }

        [Fact]
        public void Verify_RetornaFalseParaSenhaIncorreta()
        {
            var hash = _hasher.Hash("Senha@1234");
            Assert.False(_hasher.Verify("OutraSenha@1", hash));
        }

        [Fact]
        public void Hash_DuasVezesComAMesmaSenha_GeraHashesDiferentes()
        {
            // O BCrypt usa um salt aleatório a cada hash
            Assert.NotEqual(_hasher.Hash("Senha@1234"), _hasher.Hash("Senha@1234"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("1234567")]   // 7 caracteres
        public void Hash_RejeitaSenhaCurtaOuVazia(string senha)
        {
            Assert.Throws<ArgumentException>(() => _hasher.Hash(senha));
        }

        [Fact]
        public void Hash_AceitaSenhaComExatamente8Caracteres()
        {
            var hash = _hasher.Hash("12345678");
            Assert.True(_hasher.Verify("12345678", hash));
        }

        [Theory]
        [InlineData("")]
        [InlineData("não-é-um-hash")]
        public void Verify_ComHashInvalido_RetornaFalseSemLancarErro(string hashInvalido)
        {
            Assert.False(_hasher.Verify("Senha@1234", hashInvalido));
        }

        [Fact]
        public void Politica_MensagemDeErroNaoContemASenha()
        {
            var ex = Assert.Throws<ArgumentException>(() => PasswordPolicy.Garantir("abc"));
            Assert.DoesNotContain("abc", ex.Message.Replace("caracteres", ""));
        }
    }
}
