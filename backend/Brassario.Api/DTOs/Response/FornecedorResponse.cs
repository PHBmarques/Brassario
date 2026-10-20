using Brassario.Api.Models;

namespace Brassario.Api.DTOs.Response
{
    public record FornecedorResponse(long Id, string Nome, string? Contato, bool Ativo)
    {
        public static FornecedorResponse De(Fornecedor f) => new(f.Id, f.Nome, f.Contato, f.Ativo);
    }
}
