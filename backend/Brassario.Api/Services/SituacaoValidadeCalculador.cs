using Brassario.Api.Models;

namespace Brassario.Api.Services
{
    /// <summary>Regras de cor da validade. Também será usado no Armário (API-5) e nos alertas.</summary>
    public class SituacaoValidadeCalculador
    {
        public static int DiasParaVencer(DateOnly validade, DateOnly hoje) => validade.DayNumber - hoje.DayNumber;

        /// <summary>
        /// VERMELHO: faltam até "diasCritica" dias (ou já venceu). AMARELO: faltam até "diasAtencao" dias. VERDE: o resto.
        /// </summary>
        public static SituacaoValidade Calcular(DateOnly validade, DateOnly hoje, int diasCritica, int diasAtencao)
        {
            var dias = DiasParaVencer(validade, hoje);
            if (dias <= diasCritica) return SituacaoValidade.VERMELHO;
            if (dias <= diasAtencao) return SituacaoValidade.AMARELO;
            return SituacaoValidade.VERDE;
        }
    }
}
