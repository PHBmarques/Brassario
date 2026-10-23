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
        /// "Esqueci a senha": se o e-mail (de login ou de recuperação) for de uma conta ativa com recuperação confirmada,
        /// envia o link de redefinição (1 hora) para o e-mail de recuperação. Caso contrário não faz nada, em silêncio.
        /// </summary>
        Task SolicitarRedefinicaoSenhaAsync(string? email, CancellationToken ct = default);

        /// <summary>Troca a senha a partir do link. False se o token é inválido, expirou ou já foi usado. ValidacaoException para a senha.</summary>
        Task<bool> RedefinirSenhaAsync(RedefinirSenhaRequest request, CancellationToken ct = default);

        /// <summary>Troca a senha da pessoa logada (confere a atual) e abre uma sessão nova. Null se o usuário não existe ou está inativo.</summary>
        Task<SessaoEmitida?> AlterarSenhaAsync(long usuarioId, AlterarSenhaRequest request, CancellationToken ct = default);

        /// <summary>Troca o e-mail de recuperação (volta a ficar pendente) e envia o link de confirmação. Null se o usuário não existe ou está inativo.</summary>
        Task<UsuarioResponse?> AlterarEmailRecuperacaoAsync(long usuarioId, EmailRecuperacaoRequest request, CancellationToken ct = default);

        /// <summary>Revoga o refresh token do usuário (logout). Não falha se o token não existir.</summary>
        Task EncerrarSessaoAsync(long usuarioId, string? refreshToken, CancellationToken ct = default);

        Task<UsuarioResponse?> ObterUsuarioAsync(long id, CancellationToken ct = default);
    }
}
