using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Brassario.Api.Services
{
    public interface IBloqueioDeLogin
    {
        /// <summary>Tempo que falta para o login do e-mail ser liberado; null se não está bloqueado.</summary>
        TimeSpan? TempoRestante(string email);

        /// <summary>Conta uma senha errada. Ao chegar no máximo, o e-mail fica bloqueado.</summary>
        void RegistrarFalha(string email);

        /// <summary>Login correto: zera a contagem.</summary>
        void Zerar(string email);
    }

    /// <summary>
    /// Bloqueio de login por e-mail: 5 senhas erradas seguidas (padrão) bloqueiam o e-mail por 15 minutos.
    /// A contagem fica em memória. Vale igualmente para e-mail que não existe, para não revelar quais contas existem.
    /// </summary>
    public class BloqueioDeLogin : IBloqueioDeLogin
    {
        private class Estado
        {
            public int Falhas;
            public DateTime UltimaFalha;
            public DateTime? BloqueadoAte;
        }

        private readonly IRelogio _relogio;
        private readonly SegurancaOptions _opcoes;
        private readonly ConcurrentDictionary<string, Estado> _estados = new();

        public BloqueioDeLogin(IRelogio relogio, IOptions<SegurancaOptions> opcoes)
        {
            _relogio = relogio;
            _opcoes = opcoes.Value;
        }

        private TimeSpan Janela => TimeSpan.FromMinutes(Math.Max(1, _opcoes.LoginBloqueioMinutos));
        private static string Chave(string email) => email.Trim().ToLowerInvariant();

        public TimeSpan? TempoRestante(string email)
        {
            if (!_estados.TryGetValue(Chave(email), out var estado)) return null;
            var agora = _relogio.AgoraUtc;

            lock (estado)
            {
                if (estado.BloqueadoAte is { } ate && ate > agora)
                    return ate - agora;
            }
            return null;
        }

        public void RegistrarFalha(string email)
        {
            var agora = _relogio.AgoraUtc;
            var estado = _estados.GetOrAdd(Chave(email), _ => new Estado());

            lock (estado)
            {
                // Bloqueio que já acabou ou falhas muito antigas: recomeça a contagem
                if (estado.BloqueadoAte is { } ate && ate <= agora)
                {
                    estado.Falhas = 0;
                    estado.BloqueadoAte = null;
                }
                if (estado.Falhas > 0 && agora - estado.UltimaFalha >= Janela)
                    estado.Falhas = 0;

                estado.Falhas++;
                estado.UltimaFalha = agora;

                if (estado.Falhas >= Math.Max(1, _opcoes.LoginFalhasMaximas))
                    estado.BloqueadoAte = agora + Janela;
            }

            if (_estados.Count > 5000)
                Limpar(agora);
        }

        public void Zerar(string email) => _estados.TryRemove(Chave(email), out _);

        // Evita que e-mails que nunca voltaram acumulem na memória
        private void Limpar(DateTime agora)
        {
            foreach (var par in _estados)
            {
                lock (par.Value)
                {
                    var bloqueado = par.Value.BloqueadoAte is { } ate && ate > agora;
                    if (!bloqueado && agora - par.Value.UltimaFalha >= Janela)
                        _estados.TryRemove(par.Key, out _);
                }
            }
        }
    }
}
