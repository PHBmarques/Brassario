using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;

namespace Brassario.Api.Services
{
    public record SessaoEmitida(LoginResponse Resposta, string RefreshToken, DateTime RefreshExpiraEm);

    public interface IAuthService
    {
        /// <summary>Confere e-mail e senha e abre a sessão (access token + refresh token). Null se a credencial é inválida ou o usuário está inativo.</summary>
        Task<SessaoEmitida?> IniciarSessaoAsync(LoginRequest request, CancellationToken ct = default);

        /// <summary>
        /// Troca um refresh token válido por um novo par de tokens (rotação: o antigo é revogado).
        /// Null se o token não existe, expirou, foi revogado ou o usuário está inativo.
        /// </summary>
        Task<SessaoEmitida?> RenovarSessaoAsync(string? refreshToken, CancellationToken ct = default);

        /// <summary>
        /// Primeiro acesso: define a senha nova (e, se vier, o e-mail de recuperação) e abre uma sessão nova.
        /// Null se o usuário não existe ou está inativo. Lança NegocioException se a troca não está pendente
        /// e ValidacaoException com todos os erros de campo.
        /// </summary>
        Task<SessaoEmitida?> ConcluirPrimeiroAcessoAsync(long usuarioId, PrimeiroAcessoRequest request, CancellationToken ct = default);

        /// <summary>Confirma o e-mail de recuperação. False se o token é inválido, expirou ou já foi usado.</summary>
        Task<bool> ConfirmarEmailAsync(string? token, CancellationToken ct = default);

        /// <summary>
        /// Troca a senha do usuário logado conferindo a atual. Revoga os outros refresh tokens (o da sessão atual,
        /// informado em refreshTokenAtual, continua). False se o usuário não existe ou está inativo.
        /// Lança ValidacaoException (campos) e NegocioException SENHA_ATUAL_INCORRETA.
        /// </summary>
        Task<bool> AlterarSenhaAsync(long usuarioId, AlterarSenhaRequest request, string? refreshTokenAtual, CancellationToken ct = default);

        /// <summary>
        /// Troca o e-mail de recuperação: zera a confirmação, invalida links pendentes e envia um link novo para o
        /// endereço novo. O antigo deixa de valer na hora. False se o usuário não existe ou está inativo.
        /// </summary>
        Task<bool> AlterarEmailRecuperacaoAsync(long usuarioId, AlterarEmailRecuperacaoRequest request, CancellationToken ct = default);

        /// <summary>Revoga o refresh token do usuário (logout). Não falha se o token não existir.</summary>
        Task EncerrarSessaoAsync(long usuarioId, string? refreshToken, CancellationToken ct = default);

        Task<UsuarioResponse?> ObterUsuarioAsync(long id, CancellationToken ct = default);

        /// <summary>A conta do usuário logado, com o e-mail de recuperação. Null se não existe ou está inativo.</summary>
        Task<MinhaContaResponse?> ObterMinhaContaAsync(long id, CancellationToken ct = default);
    }
}
