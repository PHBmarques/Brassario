using Brassario.Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Brassario.Tests
{
    /// <summary>O usuário da aplicação lê e grava dados, mas não altera o esquema (PostgreSQL real).</summary>
    [Trait("Categoria", "Docker")]
    public class PrivilegioMinimoPostgresTests : IClassFixture<ConcorrenciaPostgresFixture>
    {
        private const string Usuario = "brassario_app_teste";
        private const string Senha = "Senha'Com\"aspas@123";

        private readonly ConcorrenciaPostgresFixture _banco;

        public PrivilegioMinimoPostgresTests(ConcorrenciaPostgresFixture banco) => _banco = banco;

        private NpgsqlConnection ComoUsuarioApp()
        {
            var b = new NpgsqlConnectionStringBuilder(_banco.ConnectionString) { Username = Usuario, Password = Senha, Pooling = false };
            return new NpgsqlConnection(b.ConnectionString);
        }

        private static async Task<int> Executar(NpgsqlConnection c, string sql)
        {
            await using var cmd = new NpgsqlCommand(sql, c);
            return await cmd.ExecuteNonQueryAsync();
        }

        [Fact]
        public async Task Usuario_da_aplicacao_le_e_grava_dados_mas_nao_altera_o_esquema()
        {
            await using (var dono = _banco.NovoContexto())
            {
                await PrivilegioMinimo.GarantirUsuarioDaAplicacaoAsync(dono, Usuario, Senha);
                await PrivilegioMinimo.GarantirUsuarioDaAplicacaoAsync(dono, Usuario, Senha);   // repetir é seguro
            }

            await using var c = ComoUsuarioApp();
            await c.OpenAsync();

            // Dados: pode
            Assert.Equal(1, await Executar(c, "INSERT INTO fornecedor (nome, ativo) VALUES ('Fornecedor do teste de privilegio', true)"));
            Assert.Equal(1, await Executar(c, "UPDATE fornecedor SET contato = 'x' WHERE nome = 'Fornecedor do teste de privilegio'"));
            Assert.Equal(1, await Executar(c, "DELETE FROM fornecedor WHERE nome = 'Fornecedor do teste de privilegio'"));

            // Esquema: não pode (42501 = insufficient_privilege)
            foreach (var ddl in new[]
            {
                "CREATE TABLE tabela_indevida (id int)",
                "ALTER TABLE insumo ADD COLUMN coluna_indevida int",
                "DROP TABLE fornecedor",
                "TRUNCATE TABLE fornecedor CASCADE",
                "SELECT * FROM \"__EFMigrationsHistory\""
            })
            {
                var ex = await Assert.ThrowsAsync<PostgresException>(() => Executar(c, ddl));
                Assert.Equal("42501", ex.SqlState);
            }
        }

        [Theory]
        [InlineData("Brassario;DROP")]
        [InlineData("usuario com espaco")]
        [InlineData("Maiuscula")]
        [InlineData("")]
        public async Task Nome_de_usuario_invalido_e_recusado_antes_de_tocar_no_banco(string nome)
        {
            await using var dono = _banco.NovoContexto();
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => PrivilegioMinimo.GarantirUsuarioDaAplicacaoAsync(dono, nome, "senha"));
        }
    }
}
