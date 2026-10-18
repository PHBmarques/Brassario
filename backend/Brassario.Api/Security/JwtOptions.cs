namespace Brassario.Api.Security
{
    public class JwtOptions
    {
        public const string Secao = "Jwt";

        public string Key { get; set; } = string.Empty;
        public string Issuer { get; set; } = "brassario";
        public int ExpiresMinutes { get; set; } = 30;
    }
}
