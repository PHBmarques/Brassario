using Brassario.Api.DTOs.Response;
using Brassario.Api.Models;

namespace Brassario.Api.Services
{
    public interface IAlertasService
    {
        Task<List<AlertaResponse>> ListarAsync(TipoAlerta? tipo, SeveridadeAlerta? severidade, CancellationToken ct = default);
    }
}
