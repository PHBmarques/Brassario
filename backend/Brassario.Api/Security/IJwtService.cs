using Brassario.Api.Models;

namespace Brassario.Api.Security
{
    public interface IJwtService
    {
        (string Token, DateTime ExpiraEm) GerarAccessToken(Usuario usuario);
    }
}
