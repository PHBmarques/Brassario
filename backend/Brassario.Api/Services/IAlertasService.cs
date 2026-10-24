using Brassario.Api.DTOs.Response;
using Brassario.Api.Models;

namespace Brassario.Api.Services
{
    public interface IAlertasService
    {
        /// <summary>
        /// Alertas de validade e de estoque com a ação de cada cartão. Filtros tipo e severidade combinam;
        /// limite (1 a 500) corta a lista depois de ordenar. Erro: 400 se o limite for inválido.
        /// </summary>
        Task<List<AlertaResponse>> ListarAsync(TipoAlerta? tipo, SeveridadeAlerta? severidade, int? limite = null, CancellationToken ct = default);
    }
}
