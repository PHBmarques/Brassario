namespace Brassario.Api.Security
{
    public class JwtOptions
    {
        public const string Secao = "Jwt";

        public string Key { get; set; } = string.Empty;
        public string Issuer { get; set; } = "brassario";
        public int ExpiresMinutes { get; set; } = 30;

        /// <summary>Validade do refresh token, em dias (AUTH-4). Padrão: 30.</summary>
        public int RefreshDays { get; set; } = 30;

        /// <summary>Nome do cookie do refresh token.</summary>
        public string CookieNome { get; set; } = "brassario_refresh";

        /// <summary>
        /// Flag Secure do cookie (a especificação exige true). Só mude para false (Jwt__CookieSecure=false)
        /// para testar em http numa rede local que não seja localhost.
        /// </summary>
        public bool CookieSecure { get; set; } = true;
    }
}
