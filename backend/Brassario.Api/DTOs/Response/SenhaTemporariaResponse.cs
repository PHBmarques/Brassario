namespace Brassario.Api.DTOs.Response
{
    /// <summary>Devolvida uma única vez: no banco fica só o hash.</summary>
    public record SenhaTemporariaResponse(string SenhaTemporaria)
    {
        // Se alguém registrar o objeto em log por engano, a senha não aparece
        public override string ToString() => "SenhaTemporariaResponse { SenhaTemporaria = *** }";
    }
}