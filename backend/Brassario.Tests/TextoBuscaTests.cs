using Brassario.Api.Services;
using Xunit;

namespace Brassario.Tests
{
    public class TextoBuscaTests
    {
        [Theory]
        [InlineData("Lúpulo", "lupulo")]
        [InlineData("AÇÚCAR", "acucar")]
        [InlineData("  São   Paulo  ", "sao paulo")]
        [InlineData("Cevada Têmpera", "cevada tempera")]
        [InlineData("ÀÁÂÃÄ éèêë íìîï óòôõö úùûü", "aaaaa eeee iiii ooooo uuuu")]
        [InlineData("", "")]
        [InlineData("   ", "")]
        public void Normaliza_sem_acento_sem_maiuscula_e_sem_espaco_sobrando(string entrada, string esperado) =>
            Assert.Equal(esperado, TextoBusca.Normalizar(entrada));

        [Fact]
        public void Nulo_vira_texto_vazio() => Assert.Equal(string.Empty, TextoBusca.Normalizar(null));

        [Fact]
        public void Funciona_com_acento_decomposto_e_composto()
        {
            var composto = "ú";          // ú
            var decomposto = "ú";       // u + acento
            Assert.Equal("u", TextoBusca.Normalizar(composto));
            Assert.Equal("u", TextoBusca.Normalizar(decomposto));
        }
    }
}
