using System.Collections.Concurrent;

namespace Brassario.Api.Services
{
    public interface ILimitadorTentativas
    {
        /// <summary>Registra uma tentativa para a chave. False se o limite da janela já foi atingido.</summary>
        bool Tentar(string chave, int maximo, TimeSpan janela);
    }

    /// <summary>Contador em memória (janela deslizante). Serve para limites que dependem do corpo da requisição.</summary>
    public class LimitadorTentativas : ILimitadorTentativas
    {
        private readonly IRelogio _relogio;
        private readonly ConcurrentDictionary<string, Queue<DateTime>> _registros = new();

        public LimitadorTentativas(IRelogio relogio) => _relogio = relogio;

        public bool Tentar(string chave, int maximo, TimeSpan janela)
        {
            var agora = _relogio.AgoraUtc;
            var fila = _registros.GetOrAdd(chave, _ => new Queue<DateTime>());

            lock (fila)
            {
                while (fila.Count > 0 && agora - fila.Peek() >= janela)
                    fila.Dequeue();

                if (fila.Count >= maximo)
                    return false;

                fila.Enqueue(agora);
            }

            if (_registros.Count > 5000)
                Limpar(agora, janela);

            return true;
        }

        // Evita que chaves antigas (e-mails que nunca voltaram) acumulem na memória
        private void Limpar(DateTime agora, TimeSpan janela)
        {
            foreach (var par in _registros)
            {
                lock (par.Value)
                {
                    if (par.Value.All(d => agora - d >= janela))
                        _registros.TryRemove(par.Key, out _);
                }
            }
        }
    }
}