namespace Brassario.Api.Services
{
    /// <summary>Data e hora atuais. Existe para os testes poderem fixar "hoje".</summary>
    public interface IRelogio
    {
        /// <summary>Dia de hoje no fuso do servidor (no Docker, America/Sao_Paulo).</summary>
        DateOnly Hoje { get; }

        /// <summary>Instante atual em UTC, para gravar a data e hora das movimentações.</summary>
        DateTime AgoraUtc { get; }
    }

    public class Relogio : IRelogio
    {
        public DateOnly Hoje => DateOnly.FromDateTime(DateTime.Now);
        public DateTime AgoraUtc => DateTime.UtcNow;
    }
}
