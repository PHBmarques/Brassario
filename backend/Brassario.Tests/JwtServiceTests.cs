using Brassario.Api.Models;
using Brassario.Api.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Xunit;

namespace Brassario.Tests
{
    public class JwtServiceTests
    {
        private static JwtService Criar(int minutos = 30) => new(Options.Create(new JwtOptions
        {
            Key = "chave-de-teste-com-mais-de-32-caracteres-ok",
            Issuer = "brassario",
            ExpiresMinutes = minutos
        }));

        private static Usuario UsuarioTeste() => new()
        {
            Id = 7,
            Nome = "Teste",
            Email = "teste@brassario.com",
            Perfil = PerfilUsuario.ADMIN
        };

        [Fact]
        public void Token_tem_id_email_e_perfil_nos_claims()
        {
            var (token, _) = Criar().GerarAccessToken(UsuarioTeste());
            var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);

            Assert.Equal("7", jwt.GetClaim("sub").Value);
            Assert.Equal("teste@brassario.com", jwt.GetClaim("email").Value);
            Assert.Equal("ADMIN", jwt.GetClaim("role").Value);
        }

        [Fact]
        public void Token_expira_em_30_minutos()
        {
            var (_, expiraEm) = Criar().GerarAccessToken(UsuarioTeste());
            var diferenca = expiraEm - DateTime.UtcNow;

            Assert.InRange(diferenca.TotalMinutes, 29, 30.1);
        }

        [Fact]
        public void Token_nao_contem_a_senha_nem_o_hash()
        {
            var u = UsuarioTeste();
            u.SenhaHash = "$2a$12$hashquenaodeveaparecer";
            var (token, _) = Criar().GerarAccessToken(u);
            var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);

            Assert.DoesNotContain("hashquenaodeveaparecer", jwt.EncodedPayload);
        }

        [Fact]
        public void Token_leva_pwd_change_quando_a_troca_de_senha_esta_pendente()
        {
            var u = UsuarioTeste();
            u.DeveTrocarSenha = true;

            var (token, _) = Criar().GerarAccessToken(u);
            var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);

            Assert.Equal("1", jwt.GetClaim("pwd_change").Value);
        }

        [Fact]
        public void Token_nao_leva_pwd_change_quando_nao_ha_troca_pendente()
        {
            var u = UsuarioTeste();
            u.DeveTrocarSenha = false;

            var (token, _) = Criar().GerarAccessToken(u);
            var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);

            Assert.DoesNotContain(jwt.Claims, c => c.Type == "pwd_change");
        }
    }
}
