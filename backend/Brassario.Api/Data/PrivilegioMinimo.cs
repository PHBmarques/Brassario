using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Brassario.Api.Data
{
    /// <summary>
    /// Privilégio mínimo no banco: a API do dia a dia usa um usuário que só lê e grava dados (SELECT, INSERT,
    /// UPDATE, DELETE) e não altera o esquema. As migrations rodam com outro usuário (o dono do banco), e este
    /// passo cria/atualiza o usuário da aplicação logo depois de migrar. É seguro repetir a cada inicialização.
    /// </summary>
    public static class PrivilegioMinimo
    {
        private static readonly Regex Identificador = new("^[a-z_][a-z0-9_]{0,62}$", RegexOptions.Compiled);

        public static async Task GarantirUsuarioDaAplicacaoAsync(
            DbContext dbDono, string usuario, string senha, ILogger? log = null, CancellationToken ct = default)
        {
            if (!Identificador.IsMatch(usuario))
                throw new InvalidOperationException(
                    "BancoApp:Usuario inválido. Use só letras minúsculas, números e sublinhado (ex.: brassario_app).");
            if (string.IsNullOrEmpty(senha))
                throw new InvalidOperationException("BancoApp:Senha ausente. Defina APP_DB_PASSWORD no .env.");

            var existe = (await dbDono.Database
                .SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM pg_roles WHERE rolname = {usuario}")
                .ToListAsync(ct)).First() > 0;
            var banco = (await dbDono.Database
                .SqlQuery<string>($"SELECT current_database() AS \"Value\"")
                .ToListAsync(ct)).First();

            var literalSenha = "'" + senha.Replace("'", "''") + "'";
            var papel = $"\"{usuario}\"";
            var nomeBanco = "\"" + banco.Replace("\"", "\"\"") + "\"";

            await Executar(dbDono,
                $"{(existe ? "ALTER" : "CREATE")} ROLE {papel} WITH LOGIN PASSWORD {literalSenha} " +
                "NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT", ct);
            await Executar(dbDono, $"GRANT CONNECT ON DATABASE {nomeBanco} TO {papel}", ct);
            await Executar(dbDono, "REVOKE CREATE ON SCHEMA public FROM PUBLIC", ct);
            await Executar(dbDono, $"GRANT USAGE ON SCHEMA public TO {papel}", ct);
            await Executar(dbDono, $"GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {papel}", ct);
            await Executar(dbDono, $"GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO {papel}", ct);
            // O histórico das migrations é só do dono do banco
            await Executar(dbDono, $"REVOKE ALL ON TABLE \"__EFMigrationsHistory\" FROM {papel}", ct);
            // Tabelas criadas por migrations futuras já nascem com o mesmo acesso
            await Executar(dbDono,
                $"ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {papel}", ct);
            await Executar(dbDono,
                $"ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO {papel}", ct);

            log?.LogInformation("Usuário de banco da aplicação '{Usuario}' configurado com privilégio mínimo.", usuario);
        }

        // ExecuteSqlRaw trata { e } como parâmetros; o SQL daqui é todo montado e validado, então basta escapá-los
        private static Task Executar(DbContext db, string sql, CancellationToken ct) =>
            db.Database.ExecuteSqlRawAsync(sql.Replace("{", "{{").Replace("}", "}}"), ct);
    }
}
