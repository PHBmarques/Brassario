using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;

namespace Brassario.Api.Services
{
    public interface IMovimentacaoService
    {
        Task<List<MovimentacaoResponse>> ListarAsync(MovimentacaoFiltro filtro, CancellationToken ct = default);
        Task<MovimentacaoResponse> RegistrarPerdaAsync(long loteId, PerdaRequest request, long usuarioId, CancellationToken ct = default);
        Task<MovimentacaoResponse> RegistrarAjusteAsync(long loteId, AjusteRequest request, long usuarioId, CancellationToken ct = default);
    }
}
