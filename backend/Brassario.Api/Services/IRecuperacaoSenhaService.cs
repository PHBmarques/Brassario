using Brassario.Api.DTOs.Request;

namespace Brassario.Api.Services
{
    public interface IRecuperacaoSenhaService
    {
        /// <summary>
        /// Pede o link de redefinição. Não devolve nada que revele se a conta existe.
        /// Lança ValidacaoException (e-mail em branco) e MuitasTentativasException (limite por e-mail).
        /// </summary>
        Task SolicitarAsync(string? emailLogin, CancellationToken ct = default);

        /// <summary>
        /// Troca a senha pelo link. False se o token é inválido, venceu ou já foi usado.
        /// Lança ValidacaoException se a senha ou a confirmação forem inválidas (o token não é consumido).
        /// </summary>
        Task<bool> RedefinirAsync(RedefinirSenhaRequest request, CancellationToken ct = default);
    }
}