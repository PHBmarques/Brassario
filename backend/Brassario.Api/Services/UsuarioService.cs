using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;
using Brassario.Api.Security;

namespace Brassario.Api.Services
{
    public class UsuarioService : IUsuarioService
    {
        private readonly IUsuarioRepository _repo;
        private readonly IPasswordHasher _hasher;

        public UsuarioService(IUsuarioRepository repo, IPasswordHasher hasher)
        {
            _repo = repo;
            _hasher = hasher;
        }

        public async Task<List<UsuarioResponse>> ListarAsync(string? busca, bool incluirInativos, CancellationToken ct = default) =>
            (await _repo.ListarAsync(busca, incluirInativos, ct)).Select(UsuarioResponse.De).ToList();

        public async Task<UsuarioResponse> ObterAsync(long id, CancellationToken ct = default) =>
            UsuarioResponse.De(await BuscarOuFalharAsync(id, ct));

        public async Task<UsuarioResponse> CriarAsync(UsuarioRequest request, CancellationToken ct = default)
        {
            var dados = Validar(request.Nome, request.Email, request.Perfil, request.Senha, senhaObrigatoria: true);
            await GarantirEmailUnicoAsync(dados.Email, null, ct);

            var usuario = new Usuario
            {
                Nome = dados.Nome,
                Email = dados.Email,
                Perfil = dados.Perfil,
                SenhaHash = _hasher.Hash(dados.Senha!),   // BCrypt: a senha em si nunca é gravada
                Ativo = true,
                CriadoEm = DateTime.UtcNow,
                DeveTrocarSenha = true,   // senha temporária: o operador troca no primeiro acesso
            };

            await _repo.AdicionarAsync(usuario, ct);
            await _repo.SalvarAsync(ct);
            return UsuarioResponse.De(usuario);
        }

        public async Task<UsuarioResponse> AtualizarAsync(long id, UsuarioAtualizarRequest request, CancellationToken ct = default)
        {
            var usuario = await BuscarOuFalharAsync(id, ct);
            var dados = Validar(request.Nome, request.Email, request.Perfil, request.Senha, senhaObrigatoria: false);
            await GarantirEmailUnicoAsync(dados.Email, id, ct);

            // Rebaixar o último ADMIN ativo deixaria o sistema sem administrador
            if (usuario.Perfil == PerfilUsuario.ADMIN && dados.Perfil != PerfilUsuario.ADMIN)
                await GarantirOutroAdminAtivoAsync(usuario, "rebaixado para OPERADOR", ct);

            usuario.Nome = dados.Nome;
            usuario.Email = dados.Email;
            usuario.Perfil = dados.Perfil;

            if (dados.Senha is not null)
            {
                usuario.SenhaHash = _hasher.Hash(dados.Senha);
                await _repo.RevogarSessoesAsync(usuario.Id, ct);   // senha nova: as sessões antigas deixam de renovar
            }

            await _repo.SalvarAsync(ct);
            return UsuarioResponse.De(usuario);
        }

        // RN-16: não existe exclusão; o usuário é apenas inativado
        public async Task<UsuarioResponse> AlterarStatusAsync(long id, bool ativo, long usuarioLogadoId, CancellationToken ct = default)
        {
            var usuario = await BuscarOuFalharAsync(id, ct);

            if (!ativo && usuario.Ativo)
            {
                if (usuario.Id == usuarioLogadoId)
                    throw new NegocioException("Você não pode inativar a sua própria conta.", "REGRA_NEGOCIO");

                if (usuario.Perfil == PerfilUsuario.ADMIN)
                    await GarantirOutroAdminAtivoAsync(usuario, "inativado", ct);
            }

            usuario.Ativo = ativo;

            // Inativo não renova mais a sessão (o access token vale até vencer, no máximo 30 minutos)
            if (!ativo)
                await _repo.RevogarSessoesAsync(usuario.Id, ct);

            await _repo.SalvarAsync(ct);
            return UsuarioResponse.De(usuario);
        }

        public async Task<SenhaTemporariaResponse> RedefinirSenhaAsync(long id, long usuarioLogadoId, CancellationToken ct = default)
        {
            var usuario = await BuscarOuFalharAsync(id, ct);

            if (usuario.Id == usuarioLogadoId)
                throw new NegocioException(
                    "Você não pode redefinir a sua própria senha por aqui. Use \"Alterar minha senha\".", "REGRA_NEGOCIO");

            var senha = GeradorSenhaTemporaria.Gerar();

            usuario.SenhaHash = _hasher.Hash(senha);   // BCrypt: só o hash é gravado
            usuario.DeveTrocarSenha = true;            // a pessoa troca a senha no próximo acesso
            await _repo.RevogarSessoesAsync(usuario.Id, ct);

            await _repo.SalvarAsync(ct);
            return new SenhaTemporariaResponse(senha);
        }

        // ---------- Apoio ----------

        private async Task<Usuario> BuscarOuFalharAsync(long id, CancellationToken ct) =>
            await _repo.ObterPorIdAsync(id, ct) ?? throw new RecursoNaoEncontradoException("Usuário", id);

        private async Task GarantirEmailUnicoAsync(string email, long? ignorarId, CancellationToken ct)
        {
            if (await _repo.ExisteEmailAsync(email, ignorarId, ct))
                throw new ConflitoException($"Já existe um usuário com o e-mail \"{email}\".");
        }

        private async Task GarantirOutroAdminAtivoAsync(Usuario usuario, string acao, CancellationToken ct)
        {
            if (!usuario.Ativo) return;   // um ADMIN já inativo não conta como administrador

            if (await _repo.ContarAdminsAtivosExcetoAsync(usuario.Id, ct) == 0)
                throw new NegocioException(
                    $"Este é o único ADMIN ativo e não pode ser {acao}. Cadastre ou ative outro ADMIN antes.", "REGRA_NEGOCIO");
        }

        private record DadosValidos(string Nome, string Email, PerfilUsuario Perfil, string? Senha);

        // Valida tudo de uma vez e devolve todos os campos com problema (400)
        private static DadosValidos Validar(string? nomeEntrada, string? emailEntrada, string? perfilEntrada, string? senhaEntrada, bool senhaObrigatoria)
        {
            var erros = new Dictionary<string, string[]>();

            var nome = (nomeEntrada ?? string.Empty).Trim();
            if (nome.Length == 0) erros["nome"] = new[] { "O nome é obrigatório." };
            else if (nome.Length > 120) erros["nome"] = new[] { "O nome deve ter no máximo 120 caracteres." };

            var email = (emailEntrada ?? string.Empty).Trim().ToLowerInvariant();
            if (email.Length == 0) erros["email"] = new[] { "O e-mail é obrigatório." };
            else if (email.Length > 160) erros["email"] = new[] { "O e-mail deve ter no máximo 160 caracteres." };
            else if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
                erros["email"] = new[] { "Informe um e-mail válido." };

            if (!Enum.TryParse<PerfilUsuario>((perfilEntrada ?? string.Empty).Trim(), true, out var perfil) || !Enum.IsDefined(perfil))
                erros["perfil"] = new[] { $"Perfil inválido. Use: {string.Join(", ", Enum.GetNames<PerfilUsuario>())}." };

            string? senha = null;
            if (!string.IsNullOrEmpty(senhaEntrada))
            {
                var problemas = PasswordPolicy.Validar(senhaEntrada);
                if (problemas.Count > 0) erros["senha"] = problemas.ToArray();
                else senha = senhaEntrada;
            }
            else if (senhaObrigatoria)
            {
                erros["senha"] = new[] { "A senha é obrigatória." };
            }

            if (erros.Count > 0) throw new ValidacaoException(erros);

            return new DadosValidos(nome, email, perfil, senha);
        }
    }
}
